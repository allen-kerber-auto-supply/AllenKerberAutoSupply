using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceRepository(FirestoreDb firestore) : IInvoiceRepository
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

        var storeRef = firestore.Collection("stores").Document(storeNumber.ToString());
        var snapshot = await storeRef.GetSnapshotAsync(cancellationToken);

        if (snapshot.Exists)
        {
            var storeRecord = snapshot.ConvertTo<StoreRecord>();
            var currentState = storeRecord.UploadState ?? new StoreUploadState();
            return new InvoiceUploadReconciliation
            {
                MissingInvoiceImages = currentState.MissingInvoiceImages ?? [],
                MissingInvoices = currentState.MissingInvoices ?? []
            };
        }

        var invoiceSnapshot = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);

        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);

        var invoiceKeys = invoiceSnapshot.Documents
            .Select(document => document.ConvertTo<Invoice>().InvoiceNumber)
            .Where(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber))
            .Select(GetNormalizedInvoiceNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var imageKeys = imageSnapshot.Documents
            .Select(document => document.ConvertTo<InvoiceImageLookup>().InvoiceNumber)
            .Where(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber))
            .Select(GetNormalizedInvoiceNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uploadState = new StoreUploadState
        {
            InvoiceKeys = invoiceKeys.ToDictionary(key => key, _ => true, StringComparer.OrdinalIgnoreCase),
            ImageKeys = imageKeys.ToDictionary(key => key, _ => true, StringComparer.OrdinalIgnoreCase),
            UpdatedAt = Timestamp.GetCurrentTimestamp()
        };
        RecomputeUploadState(uploadState);

        var newStoreRecord = new StoreRecord
        {
            StoreNumber = storeNumber,
            UploadState = uploadState
        };
        await storeRef.SetAsync(newStoreRecord, cancellationToken: cancellationToken);

        return new InvoiceUploadReconciliation
        {
            MissingInvoiceImages = uploadState.MissingInvoiceImages,
            MissingInvoices = uploadState.MissingInvoices
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

    private async Task UpdateStoreUploadStateAsync(int storeNumber, string invoiceNumber, bool isInvoice, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0 || string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return;
        }

        var normalized = GetNormalizedInvoiceNumber(invoiceNumber);
        var storeRef = firestore.Collection("stores").Document(storeNumber.ToString());

        await firestore.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(storeRef);
            var storeRecord = snapshot.Exists ? snapshot.ConvertTo<StoreRecord>() : new StoreRecord { StoreNumber = storeNumber };
            var state = storeRecord.UploadState ?? new StoreUploadState();
            state.InvoiceKeys ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            state.ImageKeys ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            if (isInvoice)
            {
                state.InvoiceKeys[normalized] = true;
            }
            else
            {
                state.ImageKeys[normalized] = true;
            }

            RecomputeUploadState(state);
            storeRecord.StoreNumber = storeNumber;
            storeRecord.UploadState = state;
            transaction.Set(storeRef, storeRecord);
        }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> FindAsync(string? invoiceNumber, string? customerNumber, CancellationToken cancellationToken)
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
            .Limit(200);

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

        Query query = firestore.Collection("invoices")
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw + "\uf8ff")
            .Limit(200);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var invoices = snapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToList();

        if (invoices.Count == 0 && !string.IsNullOrWhiteSpace(raw))
        {
            var fallbackSnapshot = await firestore.Collection("invoices")
                .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw)
                .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw + "\uf8ff")
                .Limit(200)
                .GetSnapshotAsync(cancellationToken);
            invoices = fallbackSnapshot.Documents.Select(d => d.ConvertTo<Invoice>()).ToList();
        }

        return invoices.OrderBy(i => i.InvoiceNumber).ToList();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();

        Query query = firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.CustomerNumber), customerNumber)
            .WhereGreaterThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw)
            .WhereLessThanOrEqualTo(nameof(Invoice.InvoiceNumber), raw + "\uf8ff")
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
            .Select(x => x.Trim())
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

    public async Task<bool> UpsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default)
    {
        string normalized = (invoiceNumber ?? string.Empty).Trim();
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
            HasImages = false,
            ImageObjectName = string.Empty
        };

        await docRef.SetAsync(invoice, SetOptions.MergeAll, cancellationToken);
        await UpdateStoreUploadStateAsync(storeNumber, normalized, isInvoice: true, cancellationToken);
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
