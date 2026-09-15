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
            .WhereEqualTo(nameof(Invoice.HasImages), false)
            .GetSnapshotAsync(cancellationToken);
        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .WhereEqualTo(nameof(InvoiceImageLookup.HasInvoice), false)
            .GetSnapshotAsync(cancellationToken);

        var missingInvoiceImages = invoiceSnapshot.Documents
            .Select(document => document.ConvertTo<Invoice>())
            .Where(invoice => !string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            .ToList();
        var missingInvoices = imageSnapshot.Documents
            .Select(document => document.ConvertTo<InvoiceImageLookup>().InvoiceNumber)
            .Where(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(invoiceNumber => invoiceNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new InvoiceUploadReconciliation
        {
            MissingInvoiceImages = missingInvoiceImages
                .Select(invoice => new InvoiceUploadMissingImage
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate?.ToDateTime(),
                    CustomerName = invoice.CustomerName,
                    InvoiceAmount = invoice.InvoiceAmount
                })
                .OrderBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MissingInvoiceImageKeys = missingInvoiceImages
                .Select(invoice => Normalize(invoice.InvoiceNumber))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(invoiceNumber => invoiceNumber, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MissingInvoices = missingInvoices
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
