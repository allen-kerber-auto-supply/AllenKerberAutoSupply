using System.Text;
using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply.Options;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreMisreadBarcodeStore(
    FirestoreDb firestore,
    StorageClient storageClient,
    IOptions<GoogleCloudOptions> gcpOptions) : IMisreadBarcodeStore
{
    private const string ObjectPrefix = "misread_barcodes/";
    private const string StorageIdPrefix = "storage_";

    public async Task<string> SaveAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (imageStream.CanSeek)
            imageStream.Position = 0;

        var createdUtc = DateTime.UtcNow;
        var extension = Path.GetExtension(fileName);
        var filename = createdUtc.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(extension))
            extension = contentType == "application/pdf" ? ".pdf" : ".png";

        var objectName = $"{ObjectPrefix}{filename}{extension}";
        var bucketName = gcpOptions.Value.ImageBucket;
        var contentTypeValue = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;

        await storageClient.UploadObjectAsync(
            bucketName,
            objectName,
            contentTypeValue,
            imageStream,
            cancellationToken: cancellationToken);

        var record = new MisreadBarcodeRecord
        {
            Id = filename,
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"{filename}{extension}" : fileName,
            ObjectName = objectName,
            BucketName = bucketName,
            ContentType = contentTypeValue,
            CreatedUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc))
        };

        await firestore.Collection("misread_barcodes")
            .Document(record.Id)
            .SetAsync(record, cancellationToken: cancellationToken);
        return record.Id;
    }

    public async Task<List<MisreadBarcodeRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("misread_barcodes")
            .GetSnapshotAsync(cancellationToken);

        var indexedRecordsByObjectName = new Dictionary<string, MisreadBarcodeRecord>(StringComparer.Ordinal);
        foreach (var document in snapshot.Documents)
        {
            var record = document.ConvertTo<MisreadBarcodeRecord>();
            record.Id = document.Id;
            if (!string.IsNullOrWhiteSpace(record.ObjectName))
                indexedRecordsByObjectName[record.ObjectName] = record;
        }

        var bucketName = gcpOptions.Value.ImageBucket;
        var objects = storageClient.ListObjectsAsync(bucketName, ObjectPrefix);
        var records = new List<MisreadBarcodeRecord>();
        await foreach (var storageObject in objects.WithCancellation(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(storageObject.Name) ||
                storageObject.Name.EndsWith("/", StringComparison.Ordinal))
                continue;

            if (indexedRecordsByObjectName.TryGetValue(storageObject.Name, out var indexedRecord))
            {
                records.Add(indexedRecord);
            }
            else
            {
                records.Add(new MisreadBarcodeRecord
                {
                    Id = CreateStorageId(storageObject.Name),
                    FileName = Path.GetFileName(storageObject.Name),
                    ObjectName = storageObject.Name,
                    BucketName = storageObject.Bucket ?? bucketName,
                    ContentType = storageObject.ContentType ?? "image/png",
                    CreatedUtc = storageObject.UpdatedDateTimeOffset.HasValue
                        ? Timestamp.FromDateTimeOffset(storageObject.UpdatedDateTimeOffset.Value)
                        : null
                });
            }
        }

        return records
            .OrderByDescending(record => record.CreatedUtc?.ToDateTime() ?? DateTime.MinValue)
            .ToList();
    }

    public async Task<MisreadBarcodeRecord?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var snapshot = await firestore.Collection("misread_barcodes")
            .Document(id)
            .GetSnapshotAsync(cancellationToken);
        if (snapshot.Exists)
        {
            var record = snapshot.ConvertTo<MisreadBarcodeRecord>();
            record.Id = snapshot.Id;
            return record;
        }

        if (!TryGetObjectName(id, out var objectName))
            return null;

        try
        {
            var storageObject = await storageClient.GetObjectAsync(
                gcpOptions.Value.ImageBucket,
                objectName,
                cancellationToken: cancellationToken);
            return new MisreadBarcodeRecord
            {
                Id = id,
                FileName = Path.GetFileName(objectName),
                ObjectName = objectName,
                BucketName = storageObject.Bucket ?? gcpOptions.Value.ImageBucket,
                ContentType = storageObject.ContentType ?? "image/png",
                CreatedUtc = storageObject.UpdatedDateTimeOffset.HasValue
                    ? Timestamp.FromDateTimeOffset(storageObject.UpdatedDateTimeOffset.Value)
                    : null
            };
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Stream?> GetStreamAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(id, cancellationToken);
        if (record is null || string.IsNullOrWhiteSpace(record.ObjectName))
            return null;

        var stream = new MemoryStream();
        try
        {
            await storageClient.DownloadObjectAsync(
                record.BucketName,
                record.ObjectName,
                stream,
                cancellationToken: cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var record = await FindAsync(id, cancellationToken);
        if (record is null)
        {
            await firestore.Collection("misread_barcodes")
                .Document(id)
                .DeleteAsync(cancellationToken: cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(record.ObjectName))
        {
            try
            {
                await storageClient.DeleteObjectAsync(
                    record.BucketName,
                    record.ObjectName,
                    cancellationToken: cancellationToken);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }

        await firestore.Collection("misread_barcodes")
            .Document(id)
            .DeleteAsync(cancellationToken: cancellationToken);
    }

    private static string CreateStorageId(string objectName)
    {
        var encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(objectName))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return StorageIdPrefix + encodedName;
    }

    private static bool TryGetObjectName(string id, out string objectName)
    {
        objectName = string.Empty;
        if (!id.StartsWith(StorageIdPrefix, StringComparison.Ordinal))
            return false;

        var encodedName = id[StorageIdPrefix.Length..]
            .Replace('-', '+')
            .Replace('_', '/');
        encodedName += new string('=', (4 - encodedName.Length % 4) % 4);

        try
        {
            objectName = Encoding.UTF8.GetString(Convert.FromBase64String(encodedName));
            return objectName.StartsWith(ObjectPrefix, StringComparison.Ordinal) &&
                !objectName.EndsWith("/", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            objectName = string.Empty;
            return false;
        }
    }
}
