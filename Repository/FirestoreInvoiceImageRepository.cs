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
                    InvoiceNumberNormalizer.NormalizeForComparison(image.InvoiceNumber),
                    InvoiceNumberNormalizer.NormalizeForComparison(raw),
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








    

    

    public async Task<string> InsertInvoiceImageAsync(string invoiceNumber, int storeNumber, Stream imageStream, string contentType, bool invoiceOnly, int? pageIndex = null, CancellationToken cancellationToken = default)
    {
        string normalized = InvoiceNumberNormalizer.NormalizeForStorage(invoiceNumber);

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
        var matchingImageReferences = await GetMatchingImageReferencesAsync(normalized, storeNumber, docRef, cancellationToken);
        var invoiceCollection = firestore.Collection("invoices");
        var directInvoiceDocument = invoiceCollection.Document(imageDocId);
        DocumentReference? invoiceDocumentReference = directInvoiceDocument;
        var directInvoiceSnapshot = await directInvoiceDocument.GetSnapshotAsync(cancellationToken);
        if (!directInvoiceSnapshot.Exists)
        {
            var storeInvoices = await invoiceCollection
                .WhereEqualTo(nameof(Invoice.StoreNumber), storeNumber)
                .GetSnapshotAsync(cancellationToken);
            var normalizedInvoiceNumber = InvoiceNumberNormalizer.NormalizeForComparison(normalized);
            invoiceDocumentReference = storeInvoices.Documents
                .FirstOrDefault(document => string.Equals(
                    InvoiceNumberNormalizer.NormalizeForComparison(document.ConvertTo<Invoice>().InvoiceNumber),
                    normalizedInvoiceNumber,
                    StringComparison.OrdinalIgnoreCase))
                ?.Reference;
        }

        string bucketName = gcpOptions.Value.ImageBucket;
        string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;
        string finalObjectName = string.Empty;

        await firestore.RunTransactionAsync(async transaction =>
        {
            var matchingImages = await ReadMatchingImageLookupsAsync(transaction, matchingImageReferences, docRef);
            var invoiceSnapshot = invoiceDocumentReference is null
                ? null
                : await transaction.GetSnapshotAsync(invoiceDocumentReference);
            var hasInvoice = invoiceSnapshot?.Exists == true;
            var lookup = MergeInvoiceImageLookups(matchingImages, storeNumber, normalized, bucketName, resolvedContentType);

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
            lookup.HasInvoice = lookup.HasInvoice || hasInvoice;
            lookup.BucketName = bucketName;
            lookup.ContentType = resolvedContentType;
            lookup.ObjectName = finalObjectName;
            lookup.UploadedAt = Timestamp.GetCurrentTimestamp();

            transaction.Set(docRef, lookup, SetOptions.Overwrite);
            DeleteDuplicateImageDocuments(transaction, matchingImages, docRef);

            if (invoiceDocumentReference is not null && invoiceSnapshot?.Exists == true)
            {
                transaction.Update(invoiceDocumentReference, new Dictionary<string, object>
                {
                    { nameof(Invoice.HasImages), true },
                    { nameof(Invoice.ImageObjectName), finalObjectName }
                });
            }
        }, cancellationToken: cancellationToken);

        return finalObjectName;
    }

    public async Task<InvoiceImageLookup> UpsertInvoiceLookupAsync(string invoiceNumber, int storeNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0 || string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("A store number and invoice number are required.");

        string normalized = InvoiceNumberNormalizer.NormalizeForStorage(invoiceNumber);
        string documentId = $"{storeNumber}_{normalized}";
        var documentReference = firestore.Collection("invoice_images").Document(documentId);
        var matchingReferences = await GetMatchingImageReferencesAsync(normalized, storeNumber, documentReference, cancellationToken);
        InvoiceImageLookup lookup = new();

        await firestore.RunTransactionAsync(async transaction =>
        {
            var matchingImages = await ReadMatchingImageLookupsAsync(transaction, matchingReferences, documentReference);
            lookup = MergeInvoiceImageLookups(matchingImages, storeNumber, normalized, gcpOptions.Value.ImageBucket, "image/png");
            lookup.HasInvoice = true;
            transaction.Set(documentReference, lookup, SetOptions.Overwrite);
            DeleteDuplicateImageDocuments(transaction, matchingImages, documentReference);
        }, cancellationToken: cancellationToken);

        return lookup;
    }

    private async Task<IReadOnlyList<DocumentReference>> GetMatchingImageReferencesAsync(
        string invoiceNumber,
        int storeNumber,
        DocumentReference canonicalReference,
        CancellationToken cancellationToken)
    {
        var snapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var references = snapshot.Documents
            .Where(document => string.Equals(
                InvoiceNumberNormalizer.NormalizeForComparison(
                    document.ConvertTo<InvoiceImageLookup>().InvoiceNumber),
                InvoiceNumberNormalizer.NormalizeForComparison(invoiceNumber),
                StringComparison.OrdinalIgnoreCase))
            .Select(document => document.Reference)
            .ToList();

        if (references.All(reference => reference.Id != canonicalReference.Id))
            references.Add(canonicalReference);

        return references;
    }

    private static async Task<IReadOnlyList<(DocumentSnapshot Snapshot, InvoiceImageLookup Lookup)>> ReadMatchingImageLookupsAsync(
        Transaction transaction,
        IReadOnlyList<DocumentReference> references,
        DocumentReference canonicalReference)
    {
        var results = new List<(DocumentSnapshot Snapshot, InvoiceImageLookup Lookup)>();
        foreach (var reference in references)
        {
            var snapshot = await transaction.GetSnapshotAsync(reference);
            if (snapshot.Exists)
                results.Add((snapshot, snapshot.ConvertTo<InvoiceImageLookup>()));
        }

        return results
            .OrderByDescending(item => item.Snapshot.Id == canonicalReference.Id)
            .ToList();
    }

    internal static InvoiceImageLookup MergeInvoiceImageLookups(
        IReadOnlyList<(DocumentSnapshot Snapshot, InvoiceImageLookup Lookup)> matchingImages,
        int storeNumber,
        string invoiceNumber,
        string bucketName,
        string contentType)
    {
        var lookup = matchingImages.Count > 0
            ? matchingImages[0].Lookup
            : new InvoiceImageLookup
            {
                StoreNumber = storeNumber,
                InvoiceNumber = invoiceNumber,
                BucketName = bucketName,
                ContentType = contentType,
                Pages = []
            };

        lookup.StoreNumber = storeNumber;
        lookup.InvoiceNumber = invoiceNumber;
        lookup.Pages ??= [];

        foreach (var item in matchingImages)
        {
            foreach (var page in item.Lookup.Pages ?? [])
            {
                if (lookup.Pages.All(existing => existing.PageIndex != page.PageIndex))
                    lookup.Pages.Add(page);
            }

            if (!string.IsNullOrWhiteSpace(item.Lookup.ObjectName) &&
                lookup.Pages.All(page => page.ObjectName != item.Lookup.ObjectName))
            {
                lookup.Pages.Add(new InvoiceImagePage
                {
                    PageIndex = lookup.Pages.Count == 0 ? 1 : lookup.Pages.Max(page => page.PageIndex) + 1,
                    ObjectName = item.Lookup.ObjectName,
                    BucketName = item.Lookup.BucketName,
                    ContentType = item.Lookup.ContentType,
                    UploadedAt = item.Lookup.UploadedAt
                });
            }

            lookup.HasInvoice |= item.Lookup.HasInvoice;
        }

        lookup.Pages = lookup.Pages.OrderBy(page => page.PageIndex).ToList();
        if (lookup.Pages.Count > 0)
            lookup.TotalPages = lookup.Pages.Count;

        return lookup;
    }

    internal static void DeleteDuplicateImageDocuments(
        Transaction transaction,
        IReadOnlyList<(DocumentSnapshot Snapshot, InvoiceImageLookup Lookup)> matchingImages,
        DocumentReference canonicalReference)
    {
        foreach (var item in matchingImages)
        {
            if (item.Snapshot.Id != canonicalReference.Id)
                transaction.Delete(item.Snapshot.Reference);
        }
    }

    public async Task DeleteInvoiceImagesAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (storeNumber <= 0 || string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("A store number and invoice number are required.");

        var normalized = InvoiceNumberNormalizer.NormalizeForComparison(invoiceNumber);
        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var matches = imageSnapshot.Documents
            .Where(document => string.Equals(
                InvoiceNumberNormalizer.NormalizeForComparison(document.ConvertTo<InvoiceImageLookup>().InvoiceNumber),
                normalized,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var objects = matches.SelectMany(document =>
        {
            var lookup = document.ConvertTo<InvoiceImageLookup>();
            return lookup.Pages.Select(page => (Bucket: page.BucketName, Name: page.ObjectName))
                .Append((Bucket: lookup.BucketName, Name: lookup.ObjectName));
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
        var current = InvoiceNumberNormalizer.NormalizeForStorage(currentInvoiceNumber);
        var replacement = InvoiceNumberNormalizer.NormalizeForStorage(newInvoiceNumber);
        if (storeNumber <= 0 || current.Length == 0 || replacement.Length == 0)
            throw new ArgumentException("A store number and both invoice numbers are required.");

        var imageSnapshot = await firestore.Collection("invoice_images")
            .WhereEqualTo(nameof(InvoiceImageLookup.StoreNumber), storeNumber)
            .GetSnapshotAsync(cancellationToken);
        var sourceDocuments = imageSnapshot.Documents
            .Where(document => string.Equals(
                InvoiceNumberNormalizer.NormalizeForComparison(GetStoredInvoiceNumber(document)),
                InvoiceNumberNormalizer.NormalizeForComparison(current),
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
                InvoiceNumberNormalizer.NormalizeForComparison(document.ConvertTo<Invoice>().InvoiceNumber),
                InvoiceNumberNormalizer.NormalizeForComparison(current),
                StringComparison.OrdinalIgnoreCase));
        var replacementInvoiceDocument = invoiceSnapshot.Documents.FirstOrDefault(document =>
            string.Equals(
                InvoiceNumberNormalizer.NormalizeForComparison(document.ConvertTo<Invoice>().InvoiceNumber),
                InvoiceNumberNormalizer.NormalizeForComparison(replacement),
                StringComparison.OrdinalIgnoreCase));
        lookup.HasInvoice = replacementInvoiceDocument is not null;
        await firestore.RunTransactionAsync(async transaction =>
        {
            transaction.Set(targetReference, lookup, SetOptions.Overwrite);
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

    private static string GetStoredInvoiceNumber(DocumentSnapshot document)
    {
        var values = document.ToDictionary();
        return values.TryGetValue(nameof(InvoiceImageLookup.InvoiceNumber), out var value)
            ? value?.ToString()?.Trim() ?? string.Empty
            : string.Empty;
    }

}
