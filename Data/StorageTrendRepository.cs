using System.Text;
using System.Text.Json;
using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply.Options;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace AllenKerberAutoSupply.Data;

public sealed class StorageTrendRepository(
    StorageClient storageClient,
    IOptions<GoogleCloudOptions> gcpOptions,
    ILogger<StorageTrendRepository> logger) : ITrendRepository
{
    private const string ObjectPrefix = "trends/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TrendData?> FindAsync(
        string rangeKey,
        CancellationToken cancellationToken = default)
    {
        await using var content = new MemoryStream();
        try
        {
            await storageClient.DownloadObjectAsync(
                gcpOptions.Value.ImageBucket,
                GetObjectName(rangeKey),
                content,
                cancellationToken: cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        content.Position = 0;
        try
        {
            return await JsonSerializer.DeserializeAsync<TrendData>(
                content,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Trend cache object {ObjectName} contains invalid JSON and will be regenerated.",
                GetObjectName(rangeKey));
            return null;
        }
    }

    public async Task<TrendData> CreateOrGetAsync(
        TrendData trend,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(trend.RangeKey, cancellationToken);
        if (existing is not null
            && string.Equals(existing.FromDate, trend.FromDate, StringComparison.Ordinal)
            && string.Equals(existing.ToDate, trend.ToDate, StringComparison.Ordinal)
            && string.Equals(existing.PriorYearFromDate, trend.PriorYearFromDate, StringComparison.Ordinal)
            && string.Equals(existing.PriorYearToDate, trend.PriorYearToDate, StringComparison.Ordinal)
            && string.Equals(existing.AggregateBy, trend.AggregateBy, StringComparison.Ordinal)
            && string.Equals(existing.AnalysisVersion, trend.AnalysisVersion, StringComparison.Ordinal))
            return existing;

        await using var content = new MemoryStream();
        await JsonSerializer.SerializeAsync(content, trend, JsonOptions, cancellationToken);
        content.Position = 0;
        await storageClient.UploadObjectAsync(
            gcpOptions.Value.ImageBucket,
            GetObjectName(trend.RangeKey),
            "application/json",
            content,
            cancellationToken: cancellationToken);
        return trend;
    }

    private static string GetObjectName(string rangeKey) =>
        $"{ObjectPrefix}{rangeKey}.json";
}
