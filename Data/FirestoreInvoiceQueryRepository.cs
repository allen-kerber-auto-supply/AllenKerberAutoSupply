using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceQueryRepository(FirestoreDb firestore) : IInvoiceQueryRepository
{
    public async Task<InvoiceSearchPage> FindAsync(string? invoiceNumber, string? customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken)
    {
        Query query = firestore.Collection("invoices");
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            string normalized = invoiceNumber.Trim();
            query = query.WhereEqualTo(nameof(Invoice.InvoiceNumber), normalized);
        }
        if (!string.IsNullOrWhiteSpace(customerNumber) && int.TryParse(customerNumber.Trim(), out int custNo))
        {
            query = query.WhereEqualTo(nameof(Invoice.CustomerNumber), custNo);
        }
        return await GetChargeInvoiceSearchPageAsync(query, sortKey, sortDirection, page, cancellationToken);
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default, int? storeNumber = null)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));

        Query query = firestore.Collection("invoices")
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp);
        if (storeNumber.HasValue)
        {
            query = query.WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber.Value);
        }
        return await GetChargeInvoiceSearchPageAsync(query, sortKey, sortDirection, page, cancellationToken);
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp);
        return await GetChargeInvoiceSearchPageAsync(query, sortKey, sortDirection, page, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesForTrendAsync(DateTime beginDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));
        var snapshot = await firestore.Collection("invoices")
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp)
            .GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(document => document.ConvertTo<Invoice>()).ToArray();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesForTrendCustomersAsync(
        IReadOnlyCollection<int> customerNumbers,
        CancellationToken cancellationToken = default)
    {
        if (customerNumbers.Count == 0)
            return [];

        var invoices = new List<Invoice>();
        foreach (var customerChunk in customerNumbers.Where(number => number > 0).Distinct().Chunk(30))
        {
            var snapshot = await firestore.Collection("invoices")
                .WhereIn(nameof(Invoice.CustomerNumber), customerChunk.Cast<object>())
                .GetSnapshotAsync(cancellationToken);
            invoices.AddRange(snapshot.Documents.Select(document => document.ConvertTo<Invoice>()));
        }

        return invoices;
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();
        if (raw.Length < 3 || !raw.All(char.IsDigit))
        {
            return new InvoiceSearchPage();
        }

        Query query = InvoiceSearchPageFactory.ApplyOrdering(firestore.Collection("invoices"), sortKey, sortDirection);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents
            .Select(d => d.ConvertTo<Invoice>())
            .Where(InvoiceRules.IsChargeInvoice)
            .Where(invoice => invoice.InvoiceNumber.Contains(raw, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return InvoiceSearchPageFactory.Create(invoices.Skip(page * InvoiceSearchPageFactory.PageSize).Take(InvoiceSearchPageFactory.PageSize), invoices.Count);
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();
        if (raw.Length < 3 || !raw.All(char.IsDigit))
        {
            return new InvoiceSearchPage();
        }

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber);
        query = InvoiceSearchPageFactory.ApplyOrdering(query, sortKey, sortDirection);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents
            .Select(d => d.ConvertTo<Invoice>())
            .Where(InvoiceRules.IsChargeInvoice)
            .Where(invoice => invoice.InvoiceNumber.Contains(raw, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return InvoiceSearchPageFactory.Create(invoices.Skip(page * InvoiceSearchPageFactory.PageSize).Take(InvoiceSearchPageFactory.PageSize), invoices.Count);
    }

    private static async Task<InvoiceSearchPage> GetChargeInvoiceSearchPageAsync(
        Query query,
        string? sortKey,
        string? sortDirection,
        int page,
        CancellationToken cancellationToken)
    {
        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents
            .Select(document => document.ConvertTo<Invoice>())
            .Where(InvoiceRules.IsChargeInvoice)
            .ToList();

        var ordered = InvoiceSearchPageFactory.ApplyOrdering(invoices, sortKey, sortDirection);
        return InvoiceSearchPageFactory.Create(
            ordered.Skip(Math.Max(0, page) * InvoiceSearchPageFactory.PageSize).Take(InvoiceSearchPageFactory.PageSize),
            invoices.Count);
    }

    public async Task<IReadOnlyList<StatementInvoiceItem>> GetStatementInvoicesAsync(int customerNumber, DateTime fromDate, DateTime toDate, string commaSeparatedInvoiceNumbers, CancellationToken cancellationToken = default)
    {
        var targetInvoiceNumbers = (commaSeparatedInvoiceNumbers ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(toDate, DateTimeKind.Utc));

        var customerDoc = await firestore.Collection("customers").Document(customerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        var customer = customerDoc.Exists ? customerDoc.ConvertTo<FirestoreCustomer>() : null;

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp);

        var snapshots = new List<QuerySnapshot>();
        if (targetInvoiceNumbers.Count == 0)
        {
            snapshots.Add(await query.GetSnapshotAsync(cancellationToken));
        }
        else
        {
            foreach (var invoiceNumberChunk in targetInvoiceNumbers.Chunk(30))
            {
                var chunkQuery = query.WhereIn(
                    nameof(Invoice.InvoiceNumber),
                    invoiceNumberChunk.Cast<object>());
                snapshots.Add(await chunkQuery.GetSnapshotAsync(cancellationToken));
            }
        }

        var results = new List<StatementInvoiceItem>();

        foreach (var doc in snapshots.SelectMany(snapshot => snapshot.Documents))
        {
            var invoice = doc.ConvertTo<Invoice>();
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

        return results.OrderBy(r => r.InvoiceNumber).ToList();
    }
}
