using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceUploadReconciliationStore(FirestoreDb firestore) : IInvoiceUploadReconciliationStore
{
    public Task ReconcileInvoiceAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return ReconcileStoreAsync(storeNumber, cancellationToken);
    }

    public Task ReconcileImageAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return ReconcileStoreAsync(storeNumber, cancellationToken);
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

    private static string Normalize(string? invoiceNumber)
    {
        var value = (invoiceNumber ?? string.Empty).Trim();
        if (value.Length == 0 || !value.All(char.IsDigit))
        {
            return value;
        }

        var withoutLeadingZeros = value.TrimStart('0');
        return withoutLeadingZeros.Length == 0 ? "0" : withoutLeadingZeros;
    }
}
