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
    IOptions<GoogleCloudOptions> gcpOptions) : IInvoiceImageRepository
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

    private async Task UpdateStoreUploadStateAsync(int storeNumber, string invoiceNumber, bool isInvoice, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0 || string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return;
        }

        var normalized = (invoiceNumber ?? string.Empty).Trim();
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

            var invoiceKeys = state.InvoiceKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var imageKeys = state.ImageKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            state.MissingInvoiceImages = invoiceKeys.Except(imageKeys).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
            state.MissingInvoices = imageKeys.Except(invoiceKeys).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
            state.UpdatedAt = Timestamp.GetCurrentTimestamp();

            storeRecord.StoreNumber = storeNumber;
            storeRecord.UploadState = state;
            transaction.Set(storeRef, storeRecord);
        }, cancellationToken: cancellationToken);
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

        string bucketName = gcpOptions.Value.ImageBucket;
        string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;
        string finalObjectName = string.Empty;

        await firestore.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef);
            var lookup = snapshot.Exists ? snapshot.ConvertTo<InvoiceImageLookup>() : new InvoiceImageLookup
            {
                StoreNumber = storeNumber,
                InvoiceNumber = normalized,
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
            lookup.UploadedAt = Timestamp.GetCurrentTimestamp();

            transaction.Set(docRef, lookup);
        }, cancellationToken: cancellationToken);

        await UpdateStoreUploadStateAsync(storeNumber, normalized, isInvoice: false, cancellationToken);

        // Update HasImages flag in invoices collection if invoice exists
        var invoiceRef = firestore.Collection("invoices").Document($"{storeNumber}_{normalized}");
        var invDoc = await invoiceRef.GetSnapshotAsync(cancellationToken);
        if (invDoc.Exists)
        {
            await invoiceRef.UpdateAsync(new Dictionary<string, object>
            {
                { nameof(Invoice.HasImages), true },
                { nameof(Invoice.ImageObjectName), $"invoices/{storeNumber}/{normalized}/page_1.png" }
            }, cancellationToken: cancellationToken);
        }

        return finalObjectName;
    }
}
