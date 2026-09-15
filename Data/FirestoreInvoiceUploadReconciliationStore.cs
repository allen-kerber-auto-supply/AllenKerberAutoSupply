using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceUploadReconciliationStore(FirestoreDb firestore) : IInvoiceUploadReconciliationStore
{
    public Task ReconcileInvoiceAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return ReconcileKeyAsync(storeNumber, invoiceNumber, isInvoice: true, cancellationToken);
    }

    public Task ReconcileImageAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return ReconcileKeyAsync(storeNumber, invoiceNumber, isInvoice: false, cancellationToken);
    }

    public async Task<InvoiceUploadReconciliation> ReconcileStoreAsync(int storeNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0)
        {
            return new InvoiceUploadReconciliation();
        }

        var invoiceSnapshot = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);

        var invoiceKeys = invoiceSnapshot.Documents
            .Select(document => document.ConvertTo<Invoice>().InvoiceNumber)
            .Select(Normalize)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imageKeys = imageSnapshot.Documents
            .Select(document => document.ConvertTo<InvoiceImageLookup>().InvoiceNumber)
            .Select(Normalize)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var state = CreateState(invoiceKeys, imageKeys);
        var storeRef = firestore.Collection("stores").Document(storeNumber.ToString());
        await storeRef.SetAsync(new StoreRecord { StoreNumber = storeNumber, UploadState = state }, cancellationToken: cancellationToken);

        return CreateResponse(state, invoiceSnapshot.Documents.Select(document => document.ConvertTo<Invoice>()));
    }

    private async Task ReconcileKeyAsync(int storeNumber, string invoiceNumber, bool isInvoice, CancellationToken cancellationToken)
    {
        var normalized = Normalize(invoiceNumber);
        if (storeNumber <= 0 || normalized.Length == 0)
        {
            return;
        }

        var storeRef = firestore.Collection("stores").Document(storeNumber.ToString());
        var invoiceRef = firestore.Collection("invoices").Document($"{storeNumber}_{normalized}");
        var imageRef = firestore.Collection("invoice_images").Document($"{storeNumber}_{normalized}");

        await firestore.RunTransactionAsync(async transaction =>
        {
            var storeSnapshot = await transaction.GetSnapshotAsync(storeRef);
            var invoiceSnapshot = await transaction.GetSnapshotAsync(invoiceRef);
            var imageSnapshot = await transaction.GetSnapshotAsync(imageRef);
            var storeRecord = storeSnapshot.Exists
                ? storeSnapshot.ConvertTo<StoreRecord>()
                : new StoreRecord { StoreNumber = storeNumber };
            var state = storeRecord.UploadState ?? new StoreUploadState();
            state.InvoiceKeys ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            state.ImageKeys ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            UpdateMembership(state.InvoiceKeys, normalized, invoiceSnapshot.Exists);
            UpdateMembership(state.ImageKeys, normalized, imageSnapshot.Exists);
            Recompute(state);
            storeRecord.StoreNumber = storeNumber;
            storeRecord.UploadState = state;
            transaction.Set(storeRef, storeRecord);
        }, cancellationToken: cancellationToken);
    }

    private static void UpdateMembership(Dictionary<string, bool> keys, string invoiceNumber, bool exists)
    {
        if (exists)
        {
            keys[invoiceNumber] = true;
        }
        else
        {
            keys.Remove(invoiceNumber);
        }
    }

    private static StoreUploadState CreateState(HashSet<string> invoiceKeys, HashSet<string> imageKeys)
    {
        var state = new StoreUploadState
        {
            InvoiceKeys = invoiceKeys.ToDictionary(key => key, _ => true, StringComparer.OrdinalIgnoreCase),
            ImageKeys = imageKeys.ToDictionary(key => key, _ => true, StringComparer.OrdinalIgnoreCase)
        };
        Recompute(state);
        return state;
    }

    private static void Recompute(StoreUploadState state)
    {
        var invoiceKeys = state.InvoiceKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imageKeys = state.ImageKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        state.MissingInvoiceImages = invoiceKeys.Except(imageKeys).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        state.MissingInvoices = imageKeys.Except(invoiceKeys).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        state.UpdatedAt = Timestamp.GetCurrentTimestamp();
    }

    private static InvoiceUploadReconciliation CreateResponse(StoreUploadState state, IEnumerable<Invoice> invoices)
    {
        var missingImageKeys = state.MissingInvoiceImages ?? [];
        return new InvoiceUploadReconciliation
        {
            MissingInvoiceImages = invoices
                .Where(invoice => missingImageKeys.Contains(Normalize(invoice.InvoiceNumber), StringComparer.OrdinalIgnoreCase))
                .Select(invoice => new InvoiceUploadMissingImage
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate?.ToDateTime(),
                    CustomerName = invoice.CustomerName,
                    InvoiceAmount = invoice.InvoiceAmount
                })
                .OrderBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MissingInvoiceImageKeys = missingImageKeys,
            MissingInvoices = state.MissingInvoices ?? []
        };
    }

    private static string Normalize(string? invoiceNumber) => (invoiceNumber ?? string.Empty).Trim();
}
