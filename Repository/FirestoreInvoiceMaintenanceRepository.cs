using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceMaintenanceRepository(
    FirestoreDb firestore,
    IInvoiceImageRepository invoiceImageRepository,
    IInvoiceUploadReconciliationStore reconciliationStore) : IInvoiceMaintenanceRepository
{
    public async Task<IReadOnlyList<int>> GetDistinctStoreNumbersAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("stores").GetSnapshotAsync(cancellationToken);
        var storeNumbers = new HashSet<int>();

        foreach (var document in snapshot.Documents)
        {
            if (TryGetStoreNumber(document, out var storeNumber))
            {
                storeNumbers.Add(storeNumber);
            }
        }

        return storeNumbers.OrderBy(storeNumber => storeNumber).ToArray();
    }

    private static bool TryGetStoreNumber(DocumentSnapshot document, out int storeNumber)
    {
        storeNumber = 0;

        if (!string.IsNullOrWhiteSpace(document.Id))
        {
            var idParts = document.Id.Split('_', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (idParts.Length > 0 && int.TryParse(idParts[0], out var parsedStoreNumber))
            {
                storeNumber = parsedStoreNumber;
                return storeNumber > 0;
            }
        }

        try
        {
            var invoice = document.ConvertTo<Invoice>();
            if (invoice.StoreNumber > 0)
            {
                storeNumber = invoice.StoreNumber;
                return true;
            }
        }
        catch
        {
            // fall through to no store number found
        }

        return false;
    }

    public async Task<InvoiceUploadReconciliation> GetUploadReconciliationAsync(int storeNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0)
        {
            return new InvoiceUploadReconciliation();
        }

        return await reconciliationStore.ReconcileStoreAsync(storeNumber, cancellationToken);
    }

    public async Task DeleteInvoiceAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0 || string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("A store number and invoice number are required.");

        var normalized = InvoiceNumberNormalizer.NormalizeForComparison(invoiceNumber);
        var snapshot = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var matches = snapshot.Documents
            .Where(document => string.Equals(
                InvoiceNumberNormalizer.NormalizeForComparison(document.ConvertTo<Invoice>().InvoiceNumber),
                normalized,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var document in matches)
            await document.Reference.DeleteAsync(cancellationToken: cancellationToken);

        await reconciliationStore.ReconcileStoreAsync(storeNumber, cancellationToken);
    }

    public async Task<bool> UpsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default)
    {
        string normalized = InvoiceNumberNormalizer.NormalizeForStorage(invoiceNumber);
        string docId = $"{storeNumber}_{normalized}";
        var docRef = firestore.Collection("invoices").Document(docId);

        var customerDoc = await firestore.Collection("customers").Document(customerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        string customerName = customerDoc.Exists && customerDoc.TryGetValue("CustomerName", out string name) ? name : string.Empty;

        var invoice = new Invoice
        {
            InvoiceNumber = normalized,
            StoreNumber = storeNumber,
            CustomerNumber = customerNumber,
            CustomerName = customerName,
            InvoiceDate = Timestamp.FromDateTime(DateTime.SpecifyKind(invoiceDate, DateTimeKind.Utc)),
            InvoiceAmount = (double)invoiceAmount,
            TransactionType = (transactionType ?? string.Empty).Trim(),
            PaymentMethod = (paymentMethod ?? string.Empty).Trim(),
            EmployeeNumber = employeeId,
            PoNumber = (poNumber ?? string.Empty).Trim(),
        };

        await docRef.SetAsync(invoice, SetOptions.Overwrite, cancellationToken);
        var imageLookup = await invoiceImageRepository.UpsertInvoiceLookupAsync(normalized, storeNumber, cancellationToken);
        var imageObjectName = !string.IsNullOrWhiteSpace(imageLookup.ObjectName)
            ? imageLookup.ObjectName
            : imageLookup.Pages.FirstOrDefault()?.ObjectName ?? string.Empty;
        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            { nameof(Invoice.HasImages), imageLookup.Pages.Count > 0 || imageObjectName.Length > 0 },
            { nameof(Invoice.ImageObjectName), imageObjectName }
        }, cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> InsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default)
    {
        string normalized = InvoiceNumberNormalizer.NormalizeForStorage(invoiceNumber);
        string docId = $"{storeNumber}_{normalized}";
        var docRef = firestore.Collection("invoices").Document(docId);

        var existing = await docRef.GetSnapshotAsync(cancellationToken);
        if (existing.Exists)
            return false;

        return await UpsertInvoiceDataAsync(customerNumber, normalized, invoiceDate, invoiceAmount, transactionType, employeeId, storeNumber, paymentMethod, poNumber, cancellationToken);
    }
}
