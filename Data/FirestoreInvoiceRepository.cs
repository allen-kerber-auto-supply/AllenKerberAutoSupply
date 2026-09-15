using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceRepository(
    FirestoreDb firestore,
    IInvoiceUploadReconciliationStore reconciliationStore) : IInvoiceRepository
{
    private const int SearchPageSize = 30;

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

    private async Task<List<InvoiceUploadMissingImage>> GetMissingInvoiceImageDetailsAsync(
        int storeNumber,
        IReadOnlyCollection<string> missingInvoiceKeys,
        CancellationToken cancellationToken)
    {
        if (missingInvoiceKeys.Count == 0)
        {
            return [];
        }

        var invoiceSnapshot = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);

        return invoiceSnapshot.Documents
            .Select(document => document.ConvertTo<Invoice>())
            .Where(invoice => missingInvoiceKeys.Contains(
                GetNormalizedInvoiceNumber(invoice.InvoiceNumber),
                StringComparer.OrdinalIgnoreCase))
            .Select(ToMissingInvoiceImage)
            .OrderBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static InvoiceUploadMissingImage ToMissingInvoiceImage(Invoice invoice)
    {
        return new InvoiceUploadMissingImage
        {
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate?.ToDateTime(),
            CustomerName = invoice.CustomerName,
            InvoiceAmount = invoice.InvoiceAmount
        };
    }

    private static string GetNormalizedInvoiceNumber(string? invoiceNumber)
    {
        return (invoiceNumber ?? string.Empty).Trim();
    }

    private static void RecomputeUploadState(StoreUploadState state)
    {
        var invoiceKeys = state.InvoiceKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imageKeys = state.ImageKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        state.MissingInvoiceImages = invoiceKeys.Except(imageKeys).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        state.MissingInvoices = imageKeys.Except(invoiceKeys).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        state.UpdatedAt = Timestamp.GetCurrentTimestamp();
    }

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
        var totalCount = await GetQueryCountAsync(query, cancellationToken);
        var snapshot = await ApplyInvoiceOrdering(query, sortKey, sortDirection).Offset(page * SearchPageSize).Limit(SearchPageSize).GetSnapshotAsync(cancellationToken);
        return ToSearchPage(snapshot.Documents.Select(document => document.ConvertTo<Invoice>()), totalCount);
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));

        Query query = firestore.Collection("invoices")
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp);
        var totalCount = await GetQueryCountAsync(query, cancellationToken);
        query = ApplyInvoiceOrdering(query, sortKey, sortDirection).Offset(page * SearchPageSize).Limit(SearchPageSize);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        return ToSearchPage(snapshot.Documents.Select(d => d.ConvertTo<Invoice>()), totalCount);
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default)
    {
        var startTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(beginDate, DateTimeKind.Utc));
        var endTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(endDate, DateTimeKind.Utc));

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceDate), startTimestamp)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceDate), endTimestamp);
        var totalCount = await GetQueryCountAsync(query, cancellationToken);
        query = ApplyInvoiceOrdering(query, sortKey, sortDirection).Offset(page * SearchPageSize).Limit(SearchPageSize);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        return ToSearchPage(snapshot.Documents.Select(d => d.ConvertTo<Invoice>()), totalCount);
    }

    public async Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();
        if (raw.Length < 3 || !raw.All(char.IsDigit))
        {
            return new InvoiceSearchPage();
        }

        Query query = ApplyInvoiceOrdering(firestore.Collection("invoices"), sortKey, sortDirection);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents
            .Select(d => d.ConvertTo<Invoice>())
            .Where(invoice => invoice.InvoiceNumber.Contains(raw, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ToSearchPage(invoices.Skip(page * SearchPageSize).Take(SearchPageSize), invoices.Count);
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
        query = ApplyInvoiceOrdering(query, sortKey, sortDirection);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents
            .Select(d => d.ConvertTo<Invoice>())
            .Where(invoice => invoice.InvoiceNumber.Contains(raw, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ToSearchPage(invoices.Skip(page * SearchPageSize).Take(SearchPageSize), invoices.Count);
    }

    private static async Task<int> GetQueryCountAsync(Query query, CancellationToken cancellationToken)
    {
        var countSnapshot = await query.Count().GetSnapshotAsync(cancellationToken);
        return checked((int)countSnapshot.GetValue<long>(AggregateField.Count()));
    }

    private static InvoiceSearchPage ToSearchPage(IEnumerable<Invoice> invoices, int totalCount)
    {
        var items = invoices.ToArray();
        return new InvoiceSearchPage
        {
            Items = items,
            HasMore = items.Length == SearchPageSize,
            TotalCount = totalCount
        };
    }

    private static Query ApplyInvoiceOrdering(Query query, string? sortKey, string? sortDirection)
    {
        string field = sortKey?.Trim().ToLowerInvariant() switch
        {
            "customername" => nameof(Invoice.CustomerName),
            "invoiceamount" => nameof(Invoice.InvoiceAmount),
            "storenumber" => nameof(Invoice.StoreNumber),
            _ => nameof(Invoice.InvoiceDate)
        };

        return string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(field)
            : query.OrderByDescending(field);
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

    public async Task<bool> UpsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default)
    {
        string normalized = (invoiceNumber ?? string.Empty).Trim();
        string docId = $"{storeNumber}_{normalized}";
        var docRef = firestore.Collection("invoices").Document(docId);
        var imageSnapshot = await firestore.Collection("invoice_images").Document(docId).GetSnapshotAsync(cancellationToken);
        var existingImage = imageSnapshot.Exists ? imageSnapshot.ConvertTo<InvoiceImageLookup>() : null;

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
            HasImages = existingImage is not null,
            ImageObjectName = existingImage?.ObjectName ?? string.Empty
        };

        await docRef.SetAsync(invoice, SetOptions.Overwrite, cancellationToken);
        await reconciliationStore.ReconcileInvoiceAsync(storeNumber, normalized, cancellationToken);
        return true;
    }

    public async Task<bool> InsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default)
    {
        string normalized = (invoiceNumber ?? string.Empty).Trim();
        string docId = $"{storeNumber}_{normalized}";
        var docRef = firestore.Collection("invoices").Document(docId);

        var existing = await docRef.GetSnapshotAsync(cancellationToken);
        if (existing.Exists)
            return false;

        return await UpsertInvoiceDataAsync(customerNumber, normalized, invoiceDate, invoiceAmount, transactionType, employeeId, storeNumber, paymentMethod, poNumber, cancellationToken);
    }
}
