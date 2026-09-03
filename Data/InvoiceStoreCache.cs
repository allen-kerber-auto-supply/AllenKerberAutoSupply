using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceStoreCache
{
    Task<IReadOnlyList<int>> GetStoreNumbersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> RefreshAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class InvoiceStoreCache(IDistributedCache distributedCache, IInvoiceRepository invoiceRepository) : IInvoiceStoreCache
{
    public const string StoreListCacheKey = "invoice_store_numbers";

    public async Task<IReadOnlyList<int>> GetStoreNumbersAsync(CancellationToken cancellationToken = default)
    {
        var cached = await distributedCache.GetStringAsync(StoreListCacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<int[]>(cached);
                if (parsed is { Length: > 0 })
                {
                    return parsed.Distinct().OrderBy(storeNumber => storeNumber).ToArray();
                }
            }
            catch
            {
                // Fall through to repository refresh.
            }
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var storeNumbers = await invoiceRepository.GetDistinctStoreNumbersAsync(cancellationToken);
        var ordered = storeNumbers.Distinct().OrderBy(storeNumber => storeNumber).ToArray();

        if (ordered.Length == 0)
        {
            await distributedCache.RemoveAsync(StoreListCacheKey, cancellationToken);
            return Array.Empty<int>();
        }

        var payload = JsonSerializer.Serialize(ordered);
        await distributedCache.SetStringAsync(
            StoreListCacheKey,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
            },
            cancellationToken);

        return ordered;
    }

    public Task<IReadOnlyList<int>> InitializeAsync(CancellationToken cancellationToken = default)
    {
        return GetStoreNumbersAsync(cancellationToken);
    }
}
