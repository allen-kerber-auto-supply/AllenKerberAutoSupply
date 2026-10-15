using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceQueryRepository
{
    Task<InvoiceSearchPage> FindAsync(string? invoiceNumber, string? customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default, int? storeNumber = null);
    Task<InvoiceSearchPage> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoicesForTrendAsync(DateTime beginDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoicesForTrendCustomersAsync(IReadOnlyCollection<int> customerNumbers, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementInvoiceItem>> GetStatementInvoicesAsync(int customerNumber, DateTime fromDate, DateTime toDate, string commaSeparatedInvoiceNumbers, CancellationToken cancellationToken = default);
}
