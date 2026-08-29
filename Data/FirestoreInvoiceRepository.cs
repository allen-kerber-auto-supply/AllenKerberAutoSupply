using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceRepository(FirestoreDb firestore) : IInvoiceRepository
{
    public async Task<IReadOnlyList<Invoice>> FindAsync(string? invoiceNumber, string? customerNumber, CancellationToken cancellationToken)
    {
        Query query = firestore.Collection("invoices");
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
            query = query.WhereEqualTo(nameof(Invoice.InvoiceNumber), invoiceNumber.Trim());
        if (!string.IsNullOrWhiteSpace(customerNumber))
            query = query.WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber.Trim());
        var snapshot = await query.Limit(100).GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(document => document.ConvertTo<Invoice>()).ToArray();
    }
}
