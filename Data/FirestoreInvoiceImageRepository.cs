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
        string normalized = raw.PadLeft(6, '0');

        if (storeNumber > 0)
        {
            string docId = $"{storeNumber}_{normalized}";
            var doc = await firestore.Collection("invoice_images").Document(docId).GetSnapshotAsync(cancellationToken);
            if (doc.Exists)
                return doc.ConvertTo<InvoiceImageLookup>();

            string rawDocId = $"{storeNumber}_{raw}";
            var rawDoc = await firestore.Collection("invoice_images").Document(rawDocId).GetSnapshotAsync(cancellationToken);
            if (rawDoc.Exists)
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
            string normalized = raw.PadLeft(6, '0');
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

    public async Task<string> InsertInvoiceImageAsync(string invoiceNumber, int storeNumber, Stream imageStream, string contentType, bool invoiceOnly, int? pageIndex = null, CancellationToken cancellationToken = default)
    {
        string normalized = (invoiceNumber ?? string.Empty).Trim().PadLeft(6, '0');

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
