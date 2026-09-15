using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply.Options;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceImageRepository(
    FirestoreDb firestore,
    StorageClient storageClient,
    IOptions<GoogleCloudOptions> gcpOptions,
    IInvoiceUploadReconciliationStore reconciliationStore) : IInvoiceImageRepository
{
    public async Task<InvoiceImageLookup?> GetInvoiceImageLookupAsync(string invoiceNumber, int storeNumber, CancellationToken cancellationToken = default)
    {
        string raw = (invoiceNumber ?? string.Empty).Trim();
        string normalized = raw;

        if (storeNumber > 0)
        {
            string docId = $"{storeNumber}_{normalized}";
            var doc = await GetDocumentSnapshotAsync("invoice_images", docId, cancellationToken);
            if (doc?.Exists == true)
                return doc.ConvertTo<InvoiceImageLookup>();

            string rawDocId = $"{storeNumber}_{raw}";
            var rawDoc = await GetDocumentSnapshotAsync("invoice_images", rawDocId, cancellationToken);
            if (rawDoc?.Exists == true)
                return rawDoc.ConvertTo<InvoiceImageLookup>();

            var storeImages = await firestore.Collection("invoice_images")
                .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
                .GetSnapshotAsync(cancellationToken);
            var matchingImage = storeImages.Documents
                .Select(document => document.ConvertTo<InvoiceImageLookup>())
                .FirstOrDefault(image => string.Equals(
                    NormalizeInvoiceNumber(image.InvoiceNumber),
                    NormalizeInvoiceNumber(raw),
                    StringComparison.OrdinalIgnoreCase));
            if (matchingImage is not null)
                return matchingImage;
        }

        // Search across all stores by invoice number in Firestore invoice_images collection
        var querySnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.InvoiceNumber), normalized)
            .Limit(5)
            .GetSnapshotAsync(cancellationToken);
        if (querySnapshot.Count > 0)
            return querySnapshot.Documents[0].ConvertTo<InvoiceImageLookup>();

        if (!string.IsNullOrWhiteSpace(raw) && raw != normalized)
        {
            var rawQuerySnapshot = await firestore.Collection("invoice_images")
                .WhereEqualTo(nameof(InvoiceImageLookup.InvoiceNumber), raw)
                .Limit(5)
                .GetSnapshotAsync(cancellationToken);
            if (rawQuerySnapshot.Count > 0)
                return rawQuerySnapshot.Documents[0].ConvertTo<InvoiceImageLookup>();
        }

        // Also check if the invoice record in 'invoices' has a StoreNumber
        var invQuery = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.InvoiceNumber), normalized)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);
        if (invQuery.Count > 0)
        {
            var inv = invQuery.Documents[0].ConvertTo<Invoice>();
            if (inv.StoreNumber > 0 && inv.StoreNumber != storeNumber)
            {
                var storeLookup = await GetInvoiceImageLookupAsync(invoiceNumber!, inv.StoreNumber, cancellationToken);
                if (storeLookup != null)
                    return storeLookup;
            }
        }

        // Direct Google Cloud Storage discovery fallback
        string bucket = gcpOptions.Value.ImageBucket;
        var foundPages = new List<InvoiceImagePage>();
        int discoveredStore = storeNumber > 0 ? storeNumber : 0;

        try
        {
            // List matching objects from storage with prefix invoices/
            var objects = storageClient.ListObjectsAsync(bucket, "invoices/");
            await foreach (var obj in objects.WithCancellation(cancellationToken))
            {
                // Expected format: invoices/{store}/{invoice}/page_{idx}.png
                var parts = obj.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 && parts[0] == "invoices")
                {
                    string objStore = parts[1];
                    string objInv = parts[2];
                    string filename = parts[3];

                    if (string.Equals(objInv, normalized, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(objInv, raw, StringComparison.OrdinalIgnoreCase))
                    {
                        if (discoveredStore == 0 && int.TryParse(objStore, out int parsedStore))
                            discoveredStore = parsedStore;

                        if (storeNumber <= 0 || objStore == storeNumber.ToString() || (discoveredStore > 0 && objStore == discoveredStore.ToString()))
                        {
                            int pageIdx = foundPages.Count + 1;
                            var match = Regex.Match(filename, @"page_(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedIdx))
                            {
                                pageIdx = parsedIdx;
                            }

                            foundPages.Add(new InvoiceImagePage
                            {
                                PageIndex = pageIdx,
                                ObjectName = obj.Name,
                                BucketName = bucket,
                                ContentType = obj.ContentType ?? "image/png",
                                UploadedAt = obj.UpdatedDateTimeOffset.HasValue ? Timestamp.FromDateTimeOffset(obj.UpdatedDateTimeOffset.Value) : null
                            });
                        }
                    }
                }
            }
        }
        catch { }

        if (foundPages.Count > 0)
        {
            foundPages = foundPages.OrderBy(p => p.PageIndex).ToList();
            return new InvoiceImageLookup
            {
                StoreNumber = discoveredStore > 0 ? discoveredStore : (storeNumber > 0 ? storeNumber : 302),
                InvoiceNumber = normalized,
                BucketName = bucket,
                ContentType = foundPages[0].ContentType,
                Pages = foundPages,
                TotalPages = foundPages.Count,
                ObjectName = foundPages[0].ObjectName
            };
        }

        return null;
    }

    private async Task<DocumentSnapshot?> GetDocumentSnapshotAsync(string collectionName, string documentId, CancellationToken cancellationToken)
    {
        try
        {
            return await firestore.Collection(collectionName).Document(documentId).GetSnapshotAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<Stream?> GetInvoiceImageStreamAsync(string invoiceNumber, int storeNumber, int pageIndex = 1, CancellationToken cancellationToken = default)
    {
        var lookup = await GetInvoiceImageLookupAsync(invoiceNumber, storeNumber, cancellationToken);
        string bucket = gcpOptions.Value.ImageBucket;
        string? objectName = null;

        if (lookup != null)
        {
            if (lookup.Pages.Count > 0)
            {
                var page = lookup.Pages.FirstOrDefault(p => p.PageIndex == pageIndex) ?? lookup.Pages[0];
                objectName = page.ObjectName;
            }
            else
            {
                objectName = lookup.ObjectName;
            }
            if (!string.IsNullOrWhiteSpace(lookup.BucketName))
                bucket = lookup.BucketName;
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            string raw = (invoiceNumber ?? string.Empty).Trim();
            string normalized = raw;
            var candidatePaths = new List<string>();
            if (storeNumber > 0)
            {
                candidatePaths.Add($"invoices/{storeNumber}/{normalized}/page_{pageIndex}.png");
                candidatePaths.Add($"invoices/{storeNumber}/{raw}/page_{pageIndex}.png");
            }
            candidatePaths.Add($"invoices/302/{normalized}/page_{pageIndex}.png");
            candidatePaths.Add($"invoices/302/{raw}/page_{pageIndex}.png");
            candidatePaths.Add($"invoices/1/{normalized}/page_{pageIndex}.png");
            candidatePaths.Add($"invoices/1/{raw}/page_{pageIndex}.png");

            foreach (var path in candidatePaths)
            {
                try
                {
                    var ms = new MemoryStream();
                    await storageClient.DownloadObjectAsync(bucket, path, ms, cancellationToken: cancellationToken);
                    ms.Position = 0;
                    return ms;
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Try next candidate
                }
            }
            return null;
        }

        try
        {
            var memoryStream = new MemoryStream();
            await storageClient.DownloadObjectAsync(bucket, objectName, memoryStream, cancellationToken: cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string> SaveMisreadBarcodeAsync(Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (imageStream is null)
            throw new ArgumentNullException(nameof(imageStream));

        if (imageStream.CanSeek)
            imageStream.Position = 0;

        var createdUtc = DateTime.UtcNow;
        var extension = Path.GetExtension(fileName);
        var filename = createdUtc.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(extension))
            extension = Path.GetExtension(contentType == "application/pdf" ? ".pdf" : ".png");
        var objectName = $"misread_barcodes/{filename}{extension}";
        var bucketName = gcpOptions.Value.ImageBucket;
        var contentTypeValue = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;

        await storageClient.UploadObjectAsync(bucketName, objectName, contentTypeValue, imageStream, cancellationToken: cancellationToken);

        var id = filename;
        var record = new MisreadBarcodeRecord
        {
            Id = id,
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"{filename}{extension}" : fileName,
            ObjectName = objectName,
            BucketName = bucketName,
            ContentType = contentTypeValue,
            CreatedUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc))
        };

        await firestore.Collection("misread_barcodes").Document(id).SetAsync(record, cancellationToken: cancellationToken);
        return id;
    }

    public async Task<List<MisreadBarcodeRecord>> ListMisreadBarcodesAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("misread_barcodes")
            .OrderByDescending("createdUtc")
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(document =>
            {
                var record = document.ConvertTo<MisreadBarcodeRecord>();
                record.Id = document.Id;
                return record;
            })
            .ToList();
    }

    public async Task<MisreadBarcodeRecord?> GetMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var snapshot = await firestore.Collection("misread_barcodes").Document(id).GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists)
            return null;

        var record = snapshot.ConvertTo<MisreadBarcodeRecord>();
        record.Id = snapshot.Id;
        return record;
    }

    public async Task<Stream?> GetMisreadBarcodeStreamAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await GetMisreadBarcodeAsync(id, cancellationToken);
        if (record is null || string.IsNullOrWhiteSpace(record.ObjectName))
            return null;

        var stream = new MemoryStream();
        try
        {
            await storageClient.DownloadObjectAsync(record.BucketName, record.ObjectName, stream, cancellationToken: cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<string> ResolveMisreadBarcodeAsync(string id, string invoiceNumber, int storeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A misread barcode id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("An invoice number is required.", nameof(invoiceNumber));
        if (storeNumber <= 0)
            throw new ArgumentException("A store number is required.", nameof(storeNumber));

        var record = await GetMisreadBarcodeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The selected misread barcode record no longer exists.");

        using var stream = await GetMisreadBarcodeStreamAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The selected image could not be loaded from storage.");

        stream.Position = 0;
        var destination = await InsertInvoiceImageAsync(invoiceNumber.Trim(), storeNumber, stream, record.ContentType, false, 1, cancellationToken);
        await DeleteMisreadBarcodeAsync(id, cancellationToken);
        return destination;
    }

    public async Task DeleteMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var record = await GetMisreadBarcodeAsync(id, cancellationToken);
        if (record is null)
        {
            await firestore.Collection("misread_barcodes").Document(id).DeleteAsync(cancellationToken: cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(record.ObjectName))
        {
            try
            {
                await storageClient.DeleteObjectAsync(record.BucketName, record.ObjectName, cancellationToken: cancellationToken);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }

        await firestore.Collection("misread_barcodes").Document(id).DeleteAsync(cancellationToken: cancellationToken);
    }

    public async Task<string> InsertInvoiceImageAsync(string invoiceNumber, int storeNumber, Stream imageStream, string contentType, bool invoiceOnly, int? pageIndex = null, CancellationToken cancellationToken = default)
    {
        string normalized = (invoiceNumber ?? string.Empty).Trim();

        if (invoiceOnly)
        {
            string invoiceDocId = $"{storeNumber}_{normalized}";
            var invoiceDoc = await firestore.Collection("invoices").Document(invoiceDocId).GetSnapshotAsync(cancellationToken);
            if (!invoiceDoc.Exists)
            {
                throw new InvalidOperationException($"Invoice {normalized} for store {storeNumber} does not exist.");
            }
        }

        string imageDocId = $"{storeNumber}_{normalized}";
        var docRef = firestore.Collection("invoice_images").Document(imageDocId);
        var invoiceSnapshot = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var invoiceDocument = invoiceSnapshot.Documents.FirstOrDefault(document =>
            string.Equals(
                NormalizeInvoiceNumber(document.ConvertTo<Invoice>().InvoiceNumber),
                NormalizeInvoiceNumber(normalized),
                StringComparison.OrdinalIgnoreCase));
        var hasInvoice = invoiceDocument is not null;

        string bucketName = gcpOptions.Value.ImageBucket;
        string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;
        string finalObjectName = string.Empty;
        string primaryObjectName = string.Empty;

        await firestore.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef);
            var lookup = snapshot.Exists ? snapshot.ConvertTo<InvoiceImageLookup>() : new InvoiceImageLookup
            {
                StoreNumber = storeNumber,
                InvoiceNumber = normalized,
                HasInvoice = hasInvoice,
                BucketName = bucketName,
                ContentType = resolvedContentType,
                Pages = []
            };

            int targetPage = pageIndex ?? (lookup.Pages.Count + 1);
            finalObjectName = $"invoices/{storeNumber}/{normalized}/page_{targetPage}.png";

            imageStream.Position = 0;
            await storageClient.UploadObjectAsync(
                bucketName,
                finalObjectName,
                resolvedContentType,
                imageStream,
                cancellationToken: cancellationToken);

            var page = lookup.Pages.FirstOrDefault(p => p.PageIndex == targetPage);
            if (page is null)
            {
                page = new InvoiceImagePage
                {
                    PageIndex = targetPage,
                    ObjectName = finalObjectName,
                    BucketName = bucketName,
                    ContentType = resolvedContentType,
                    UploadedAt = Timestamp.GetCurrentTimestamp()
                };
                lookup.Pages.Add(page);
            }
            else
            {
                page.ObjectName = finalObjectName;
                page.BucketName = bucketName;
                page.ContentType = resolvedContentType;
                page.UploadedAt = Timestamp.GetCurrentTimestamp();
            }

            lookup.Pages = lookup.Pages.OrderBy(p => p.PageIndex).ToList();
            lookup.TotalPages = lookup.Pages.Count;
            lookup.ObjectName = lookup.Pages[0].ObjectName;
            primaryObjectName = lookup.ObjectName;
            lookup.UploadedAt = Timestamp.GetCurrentTimestamp();

            transaction.Set(docRef, lookup);
        }, cancellationToken: cancellationToken);

        if (invoiceDocument is not null)
        {
            await invoiceDocument.Reference.UpdateAsync(new Dictionary<string, object>
            {
                { nameof(Invoice.HasImages), true },
                { nameof(Invoice.ImageObjectName), primaryObjectName }
            }, cancellationToken: cancellationToken);
        }

        await reconciliationStore.ReconcileImageAsync(storeNumber, normalized, cancellationToken);

        return finalObjectName;
    }

    public async Task DeleteInvoiceImagesAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0 || string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("A store number and invoice number are required.");

        var normalized = NormalizeInvoiceNumber(invoiceNumber);
        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var matches = imageSnapshot.Documents
            .Where(document => string.Equals(
                NormalizeInvoiceNumber(document.ConvertTo<InvoiceImageLookup>().InvoiceNumber),
                normalized,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var objects = matches.SelectMany(document =>
        {
            var lookup = document.ConvertTo<InvoiceImageLookup>();
            return lookup.Pages.Select(page => (Bucket: page.BucketName, Name: page.ObjectName))
                .Append((lookup.BucketName, lookup.ObjectName));
        })
        .Where(item => !string.IsNullOrWhiteSpace(item.Name))
        .Distinct()
        .ToList();

        foreach (var item in objects)
        {
            try
            {
                await storageClient.DeleteObjectAsync(
                    string.IsNullOrWhiteSpace(item.Bucket) ? gcpOptions.Value.ImageBucket : item.Bucket,
                    item.Name,
                    cancellationToken: cancellationToken);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }

        foreach (var document in matches)
            await document.Reference.DeleteAsync(cancellationToken: cancellationToken);

        await reconciliationStore.ReconcileStoreAsync(storeNumber, cancellationToken);
    }

    public async Task ReassignInvoiceAsync(string currentInvoiceNumber, string newInvoiceNumber, int storeNumber, CancellationToken cancellationToken = default)
    {
        var current = (currentInvoiceNumber ?? string.Empty).Trim();
        var replacement = (newInvoiceNumber ?? string.Empty).Trim();
        if (storeNumber <= 0 || current.Length == 0 || replacement.Length == 0)
            throw new ArgumentException("A store number and both invoice numbers are required.");

        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var sourceDocuments = imageSnapshot.Documents
            .Where(document => string.Equals(
                NormalizeInvoiceNumber(document.ConvertTo<InvoiceImageLookup>().InvoiceNumber),
                NormalizeInvoiceNumber(current),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sourceDocument = sourceDocuments.FirstOrDefault();
        if (sourceDocument is null)
            throw new InvalidOperationException($"No image was found for invoice {current}.");

        var targetDocumentId = $"{storeNumber}_{replacement}";
        var targetReference = firestore.Collection("invoice_images").Document(targetDocumentId);

        var lookup = sourceDocument.ConvertTo<InvoiceImageLookup>();
        lookup.InvoiceNumber = replacement;
        var invoiceSnapshot = await firestore.Collection("invoices")
            .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var currentInvoiceDocument = invoiceSnapshot.Documents.FirstOrDefault(document =>
            string.Equals(
                NormalizeInvoiceNumber(document.ConvertTo<Invoice>().InvoiceNumber),
                NormalizeInvoiceNumber(current),
                StringComparison.OrdinalIgnoreCase));
        var replacementInvoiceDocument = invoiceSnapshot.Documents.FirstOrDefault(document =>
            string.Equals(
                NormalizeInvoiceNumber(document.ConvertTo<Invoice>().InvoiceNumber),
                NormalizeInvoiceNumber(replacement),
                StringComparison.OrdinalIgnoreCase));
        lookup.HasInvoice = replacementInvoiceDocument is not null;
        await firestore.RunTransactionAsync(async transaction =>
        {
            transaction.Set(targetReference, lookup);
            foreach (var document in sourceDocuments)
            {
                if (document.Id != targetReference.Id)
                    transaction.Delete(document.Reference);
            }
            await Task.CompletedTask;
        }, cancellationToken: cancellationToken);

        if (currentInvoiceDocument is not null && currentInvoiceDocument.Id != replacementInvoiceDocument?.Id)
        {
            await currentInvoiceDocument.Reference.UpdateAsync(new Dictionary<string, object>
            {
                { nameof(Invoice.HasImages), false },
                { nameof(Invoice.ImageObjectName), string.Empty }
            }, cancellationToken: cancellationToken);
        }

        if (replacementInvoiceDocument is not null)
        {
            await replacementInvoiceDocument.Reference.UpdateAsync(new Dictionary<string, object>
            {
                { nameof(Invoice.HasImages), lookup.HasInvoice },
                { nameof(Invoice.ImageObjectName), lookup.HasInvoice ? lookup.ObjectName : string.Empty }
            }, cancellationToken: cancellationToken);
        }

        await reconciliationStore.ReconcileStoreAsync(storeNumber, cancellationToken);
    }

    private static string NormalizeInvoiceNumber(string? invoiceNumber)
    {
        var value = (invoiceNumber ?? string.Empty).Trim();
        if (value.Length == 0 || !value.All(char.IsDigit))
            return value;

        var withoutLeadingZeros = value.TrimStart('0');
        return withoutLeadingZeros.Length == 0 ? "0" : withoutLeadingZeros;
    }
}
