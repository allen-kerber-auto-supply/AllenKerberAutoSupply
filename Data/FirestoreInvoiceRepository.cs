using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreInvoiceRepository(
    IInvoiceQueryRepository queries,
    IInvoiceMaintenanceRepository maintenance) : IInvoiceRepository
{
    public Task<InvoiceSearchPage> FindAsync(string? invoiceNumber, string? customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default) =>
        queries.FindAsync(invoiceNumber, customerNumber, sortKey, sortDirection, page, cancellationToken);

    public Task<InvoiceSearchPage> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default, int? storeNumber = null) =>
        queries.GetInvoiceDataByDtmAsync(beginDate, endDate, sortKey, sortDirection, page, cancellationToken, storeNumber);

    public Task<InvoiceSearchPage> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default) =>
        queries.GetInvoiceDataByDtmAndCustomerAsync(beginDate, endDate, customerNumber, sortKey, sortDirection, page, cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetInvoicesForTrendAsync(DateTime beginDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        queries.GetInvoicesForTrendAsync(beginDate, endDate, cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetInvoicesForTrendCustomersAsync(IReadOnlyCollection<int> customerNumbers, CancellationToken cancellationToken = default) =>
        queries.GetInvoicesForTrendCustomersAsync(customerNumbers, cancellationToken);

    public Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default) =>
        queries.GetInvoiceDataByInvoiceNumberAsync(invoiceNumber, sortKey, sortDirection, page, cancellationToken);

    public Task<InvoiceSearchPage> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, string? sortKey, string? sortDirection, int page, CancellationToken cancellationToken = default) =>
        queries.GetInvoiceDataByInvoiceNumberAndCustomerAsync(invoiceNumber, customerNumber, sortKey, sortDirection, page, cancellationToken);

    public Task<IReadOnlyList<StatementInvoiceItem>> GetStatementInvoicesAsync(int customerNumber, DateTime fromDate, DateTime toDate, string commaSeparatedInvoiceNumbers, CancellationToken cancellationToken = default) =>
        queries.GetStatementInvoicesAsync(customerNumber, fromDate, toDate, commaSeparatedInvoiceNumbers, cancellationToken);

    public Task<IReadOnlyList<int>> GetDistinctStoreNumbersAsync(CancellationToken cancellationToken = default) =>
        maintenance.GetDistinctStoreNumbersAsync(cancellationToken);

    public Task<InvoiceUploadReconciliation> GetUploadReconciliationAsync(int storeNumber, CancellationToken cancellationToken = default) =>
        maintenance.GetUploadReconciliationAsync(storeNumber, cancellationToken);

    public Task DeleteInvoiceAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default) =>
        maintenance.DeleteInvoiceAsync(storeNumber, invoiceNumber, cancellationToken);

    public Task<bool> UpsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default) =>
        maintenance.UpsertInvoiceDataAsync(customerNumber, invoiceNumber, invoiceDate, invoiceAmount, transactionType, employeeId, storeNumber, paymentMethod, poNumber, cancellationToken);

    public Task<bool> InsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default) =>
        maintenance.InsertInvoiceDataAsync(customerNumber, invoiceNumber, invoiceDate, invoiceAmount, transactionType, employeeId, storeNumber, paymentMethod, poNumber, cancellationToken);
}
