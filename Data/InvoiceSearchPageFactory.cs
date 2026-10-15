using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

internal static class InvoiceSearchPageFactory
{
    public const int PageSize = 30;

    public static InvoiceSearchPage Create(IEnumerable<Invoice> invoices, int totalCount)
    {
        var items = invoices.ToArray();
        return new InvoiceSearchPage
        {
            Items = items,
            HasMore = items.Length == PageSize,
            TotalCount = totalCount
        };
    }

    public static Query ApplyOrdering(Query query, string? sortKey, string? sortDirection)
    {
        string field = sortKey?.Trim().ToLowerInvariant() switch
        {
            "customername" => nameof(Invoice.CustomerName),
            "invoiceamount" => nameof(Invoice.InvoiceAmount),
            "storenumber" => nameof(Invoice.StoreNumber),
            _ => nameof(Invoice.InvoiceDate)
        };

        return string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(field)
            : query.OrderByDescending(field);
    }

    public static IEnumerable<Invoice> ApplyOrdering(
        IEnumerable<Invoice> invoices,
        string? sortKey,
        string? sortDirection)
    {
        var ascending = string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = sortKey?.Trim().ToLowerInvariant() switch
        {
            "customername" => ascending
                ? invoices.OrderBy(invoice => invoice.CustomerName, StringComparer.OrdinalIgnoreCase)
                : invoices.OrderByDescending(invoice => invoice.CustomerName, StringComparer.OrdinalIgnoreCase),
            "invoiceamount" => ascending
                ? invoices.OrderBy(invoice => invoice.InvoiceAmount)
                : invoices.OrderByDescending(invoice => invoice.InvoiceAmount),
            "storenumber" => ascending
                ? invoices.OrderBy(invoice => invoice.StoreNumber)
                : invoices.OrderByDescending(invoice => invoice.StoreNumber),
            _ => ascending
                ? invoices.OrderBy(invoice => invoice.InvoiceDate)
                : invoices.OrderByDescending(invoice => invoice.InvoiceDate)
        };

        return ordered.ThenBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase);
    }
}
