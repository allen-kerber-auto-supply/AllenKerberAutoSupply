using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceRepository(FirestoreDb firestore) : IInvoiceRepository
{
    public async Task<IReadOnlyList<Invoice>> FindAsync(string? invoiceNumber, string? customerNumber, CancellationToken cancellationToken)
    {
        Query query = firestore.Collection("invoices");
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            string normalized = invoiceNumber.Trim().PadLeft(6, '0');
            query = query.WhereEqualTo(nameof(Invoice.InvoiceNumber), normalized);
        }
        if (!string.IsNullOrWhiteSpace(customerNumber) && int.TryParse(customerNumber.Trim(), out int custNo))
        {
            query = query.WhereEqualTo(nameof(Invoice.CustomerNumber), custNo);
        }
        var snapshot = await query.Limit(100).GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(document => document.ConvertTo<Invoice>()).ToArray();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));

        Query query = firestore.Collection("invoices")
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp)
            .OrderBy(nameof(Invoice.InvoiceDate))
            .Limit(2000);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToArray();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, CancellationToken cancellationToken = default)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp)
            .OrderBy(nameof(Invoice.InvoiceDate))
            .Limit(200);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToArray();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();
        string normalized = raw.PadLeft(6, '0');

        Query query = firestore.Collection("invoices")
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), normalized)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), normalized + "\uf8ff")
            .Limit(2000);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToList();

        if (invoices.Count == 0 && !string.IsNullOrWhiteSpace(raw))
        {
            var fallbackSnapshot = await firestore.Collection("invoices")
                .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw)
                .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw + "\uf8ff")
                .Limit(2000)
                .GetSnapshotAsync(cancellationToken);
            invoices = fallbackSnapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToList();
        }

        return invoices.OrderBy(i => i.InvoiceNumber).ToList();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();
        string normalized = raw.PadLeft(6, '0');

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), normalized)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), normalized + "\uf8ff")
            .Limit(200);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToList();

        if (invoices.Count == 0 && !string.IsNullOrWhiteSpace(raw))
        {
            var fallbackSnapshot = await firestore.Collection("invoices")
                .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
                .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw)
                .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw + "\uf8ff")
                .Limit(200)
                .GetSnapshotAsync(cancellationToken);
            invoices = fallbackSnapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToList();
        }

        return invoices.OrderBy(i => i.InvoiceNumber).ToList();
    }

    public async Task<IReadOnlyList<StatementInvoiceItem>> GetStatementInvoicesAsync(int customerNumber, DateTime fromDate, DateTime toDate, string commaSeparatedInvoiceNumbers, CancellationToken cancellationToken = default)
    {
        var targetInvoiceNumbers = (commaSeparatedInvoiceNumbers ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.PadLeft(6, '0'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(toDate, DateTimeKind.Utc));

        var customerDoc = await firestore.Collection("customers").Document(customerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        var customer = customerDoc.Exists ? customerDoc.ConvertTo<Customer>() : null;

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var results = new List<StatementInvoiceItem>();

        foreach (var doc in snapshot.Documents)
        {
            var invoice = doc.ConvertTo<Invoice>();
            if (targetInvoiceNumbers.Count == 0 || targetInvoiceNumbers.Contains(invoice.InvoiceNumber))
            {
                results.Add(new StatementInvoiceItem
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate?.ToDateTime(),
                    InvoiceAmount = invoice.InvoiceAmount,
                    VendorId = $"Vendor Number: {customer?.VendorId ?? string.Empty}",
                    StoreNumber = $"800005{invoice.StoreNumber.ToString().PadLeft(3, '0')}",
                    StatementOrInvoice = customer?.StatementOrInvoice ?? "I",
                    CustomerName = customer?.CustomerName ?? invoice.CustomerName
                });
            }
        }

        return results.OrderBy(r => r.InvoiceNumber).ToList();
    }

    public async Task<bool> InsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default)
    {
        string normalized = (invoiceNumber ?? string.Empty).Trim().PadLeft(6, '0');
        string docId = $"{storeNumber}_{normalized}";
        var docRef = firestore.Collection("invoices").Document(docId);

        var existing = await docRef.GetSnapshotAsync(cancellationToken);
        if (existing.Exists)
            return false;

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
            HasImages = false,
            ImageObjectName = string.Empty
        };

        await docRef.SetAsync(invoice, cancellationToken: cancellationToken);
        return true;
    }
}
