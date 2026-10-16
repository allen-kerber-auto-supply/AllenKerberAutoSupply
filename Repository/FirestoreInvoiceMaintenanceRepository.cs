using System.Globalization;
using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply.Options;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceMaintenanceRepository(
    FirestoreDb firestore,
    IInvoiceImageRepository invoiceImageRepository,
    IInvoiceUploadReconciliationStore reconciliationStore,
    IOptions<GoogleCloudOptions> googleCloudOptions) : IInvoiceMaintenanceRepository
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

    public async Task UpsertInvoiceDataBatchAsync(IReadOnlyList<InvoiceImportRecord> invoices, int storeNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0)
            throw new ArgumentException("A store number is required.", nameof(storeNumber));
        if (invoices.Count == 0)
            return;

        var preparedInvoices = invoices
            .Select(invoice =>
            {
                var normalized = InvoiceNumberNormalizer.NormalizeForStorage(invoice.InvoiceNumber);
                return new PreparedInvoice(
                    invoice,
                    normalized,
                    firestore.Collection("invoices").Document($"{storeNumber}_{normalized}"),
                    firestore.Collection("invoice_images").Document($"{storeNumber}_{normalized}"),
                    InvoiceNumberNormalizer.NormalizeForComparison(normalized));
            })
            .GroupBy(invoice => invoice.InvoiceReference.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();

        var customerReferences = preparedInvoices
            .Select(invoice => invoice.Record.CustomerNumber)
            .Distinct()
            .Select(customerNumber => firestore.Collection("customers").Document(customerNumber.ToString()))
            .ToArray();
        var customerSnapshots = await firestore.GetAllSnapshotsAsync(customerReferences, cancellationToken);
        var customerNames = customerSnapshots
            .Where(snapshot => snapshot.Exists)
            .ToDictionary(
                snapshot => int.Parse(snapshot.Reference.Id, CultureInfo.InvariantCulture),
                snapshot => snapshot.TryGetValue("CustomerName", out string name) ? name : string.Empty);

        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var existingLookups = imageSnapshot.Documents
            .Select(snapshot => (Snapshot: snapshot, Lookup: snapshot.ConvertTo<InvoiceImageLookup>()))
            .ToList();
        var lookupsByInvoice = existingLookups
            .GroupBy(item => InvoiceNumberNormalizer.NormalizeForComparison(item.Lookup.InvoiceNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var currentBatch = new List<PreparedInvoice>();
        var currentWriteCount = 0;
        foreach (var invoice in preparedInvoices)
        {
            if (lookupsByInvoice.TryGetValue(invoice.ComparisonKey, out var matchingLookups))
            {
                invoice.ImageReferences = matchingLookups
                    .Select(item => item.Snapshot.Reference)
                    .ToList();
            }
            if (invoice.ImageReferences.All(reference => reference.Id != invoice.ImageLookupReference.Id))
                invoice.ImageReferences.Add(invoice.ImageLookupReference);

            var additionalWrites = 2 + invoice.ImageReferences.Count(reference => reference.Id != invoice.ImageLookupReference.Id);
            if (currentBatch.Count > 0 && currentWriteCount + additionalWrites > 450)
            {
                await CommitInvoiceBatchAsync(currentBatch, customerNames, storeNumber, cancellationToken);
                currentBatch.Clear();
                currentWriteCount = 0;
            }

            currentBatch.Add(invoice);
            currentWriteCount += additionalWrites;
        }

        if (currentBatch.Count > 0)
            await CommitInvoiceBatchAsync(currentBatch, customerNames, storeNumber, cancellationToken);
    }

    private async Task CommitInvoiceBatchAsync(
        IReadOnlyList<PreparedInvoice> invoices,
        IReadOnlyDictionary<int, string> customerNames,
        int storeNumber,
        CancellationToken cancellationToken)
    {
        var imageReferences = invoices
            .SelectMany(invoice => invoice.ImageReferences)
            .DistinctBy(reference => reference.Id)
            .ToArray();

        await firestore.RunTransactionAsync(async transaction =>
        {
            var imageSnapshots = await transaction.GetAllSnapshotsAsync(imageReferences, cancellationToken);
            var snapshotsById = imageSnapshots.ToDictionary(snapshot => snapshot.Reference.Id, StringComparer.Ordinal);

            foreach (var item in invoices)
            {
                var matchingImages = item.ImageReferences
                    .Where(reference => snapshotsById.TryGetValue(reference.Id, out var snapshot) && snapshot.Exists)
                    .Select(reference => snapshotsById[reference.Id])
                    .Select(snapshot => (Snapshot: snapshot, Lookup: snapshot.ConvertTo<InvoiceImageLookup>()))
                    .OrderByDescending(image => image.Snapshot.Id == item.ImageLookupReference.Id)
                    .ToList();
                var lookup = FirestoreInvoiceImageRepository.MergeInvoiceImageLookups(
                    matchingImages,
                    storeNumber,
                    item.NormalizedInvoiceNumber,
                    googleCloudOptions.Value.ImageBucket,
                    "image/png");
                lookup.HasInvoice = true;

                transaction.Set(item.ImageLookupReference, lookup, SetOptions.Overwrite);
                FirestoreInvoiceImageRepository.DeleteDuplicateImageDocuments(
                    transaction,
                    matchingImages,
                    item.ImageLookupReference);

                var imageObjectName = !string.IsNullOrWhiteSpace(lookup.ObjectName)
                    ? lookup.ObjectName
                    : lookup.Pages.FirstOrDefault()?.ObjectName ?? string.Empty;
                var customerName = customerNames.TryGetValue(item.Record.CustomerNumber, out var name) ? name : string.Empty;
                var invoice = new Invoice
                {
                    InvoiceNumber = item.NormalizedInvoiceNumber,
                    StoreNumber = storeNumber,
                    CustomerNumber = item.Record.CustomerNumber,
                    CustomerName = customerName,
                    InvoiceDate = Timestamp.FromDateTime(DateTime.SpecifyKind(item.Record.InvoiceDate, DateTimeKind.Utc)),
                    InvoiceAmount = (double)item.Record.InvoiceAmount,
                    TransactionType = (item.Record.TransactionType ?? string.Empty).Trim(),
                    PaymentMethod = (item.Record.PaymentMethod ?? string.Empty).Trim(),
                    EmployeeNumber = item.Record.EmployeeId,
                    PoNumber = (item.Record.PoNumber ?? string.Empty).Trim(),
                    HasImages = lookup.Pages.Count > 0 || imageObjectName.Length > 0,
                    ImageObjectName = imageObjectName
                };

                transaction.Set(item.InvoiceReference, invoice, SetOptions.Overwrite);
            }
        }, cancellationToken: cancellationToken);
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

    private sealed class PreparedInvoice(
        InvoiceImportRecord record,
        string normalizedInvoiceNumber,
        DocumentReference invoiceReference,
        DocumentReference imageLookupReference,
        string comparisonKey)
    {
        public InvoiceImportRecord Record { get; } = record;
        public string NormalizedInvoiceNumber { get; } = normalizedInvoiceNumber;
        public DocumentReference InvoiceReference { get; } = invoiceReference;
        public DocumentReference ImageLookupReference { get; } = imageLookupReference;
        public string ComparisonKey { get; } = comparisonKey;
        public List<DocumentReference> ImageReferences { get; set; } = [];
    }
}
