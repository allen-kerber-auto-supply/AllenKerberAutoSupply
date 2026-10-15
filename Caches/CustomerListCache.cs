using AllenKerberAutoSupply.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AllenKerberAutoSupply.Data;

public interface ICustomerListCache
{
    Task<IReadOnlyList<CustomerSummary>> GetInvoiceCustomerListAsync(
        Func<CancellationToken, Task<IReadOnlyList<CustomerSummary>>> loader,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirestoreCustomer>> GetAdminCustomerListAsync(
        Func<CancellationToken, Task<IReadOnlyList<FirestoreCustomer>>> loader,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCustomer>> GetSalesCustomerListAsync(
        Func<CancellationToken, Task<IReadOnlyList<SalesCustomer>>> loader,
        CancellationToken cancellationToken = default);
    void InvalidateInvoiceCustomers();
    void InvalidateSalesCustomers();
}

public sealed class CustomerListCache(IMemoryCache memoryCache) : ICustomerListCache
{
    private const string InvoiceCustomerListKey = "customer-list:invoice";
    private const string AdminCustomerListKey = "customer-list:admin";
    private const string SalesCustomerListKey = "customer-list:sales";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public Task<IReadOnlyList<CustomerSummary>> GetInvoiceCustomerListAsync(
        Func<CancellationToken, Task<IReadOnlyList<CustomerSummary>>> loader,
        CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(InvoiceCustomerListKey, loader, cancellationToken);

    public Task<IReadOnlyList<FirestoreCustomer>> GetAdminCustomerListAsync(
        Func<CancellationToken, Task<IReadOnlyList<FirestoreCustomer>>> loader,
        CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(AdminCustomerListKey, loader, cancellationToken);

    public Task<IReadOnlyList<SalesCustomer>> GetSalesCustomerListAsync(
        Func<CancellationToken, Task<IReadOnlyList<SalesCustomer>>> loader,
        CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(SalesCustomerListKey, loader, cancellationToken);

    public void InvalidateInvoiceCustomers()
    {
        memoryCache.Remove(InvoiceCustomerListKey);
        memoryCache.Remove(AdminCustomerListKey);
    }

    public void InvalidateSalesCustomers() => memoryCache.Remove(SalesCustomerListKey);

    private async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> loader,
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var value = await loader(cancellationToken);
        memoryCache.Set(key, value, CacheDuration);
        return value;
    }
}