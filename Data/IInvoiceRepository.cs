using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceRepository
{
    Task<InvoiceSearchPage> FindAsync(string? invoiceNumber, string? customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementInvoiceItem>> GetStatementInvoicesAsync(int customerNumber, DateTime fromDate, DateTime toDate, string commaSeparatedInvoiceNumbers, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetDistinctStoreNumbersAsync(CancellationToken cancellationToken = default);
    Task<InvoiceUploadReconciliation> GetUploadReconciliationAsync(int storeNumber, CancellationToken cancellationToken = default);
    Task<bool> UpsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default);
    Task<bool> InsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default);
}
