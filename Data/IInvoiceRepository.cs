using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> FindAsync(string? invoiceNumber, string? customerNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoiceDataByDtmAsync(DateTime beginDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoiceDataByDtmAndCustomerAsync(DateTime beginDate, DateTime endDate, int customerNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoiceDataByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoiceDataByInvoiceNumberAndCustomerAsync(string invoiceNumber, int customerNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementInvoiceItem>> GetStatementInvoicesAsync(int customerNumber, DateTime fromDate, DateTime toDate, string commaSeparatedInvoiceNumbers, CancellationToken cancellationToken = default);
    Task<bool> InsertInvoiceDataAsync(int customerNumber, string invoiceNumber, DateTime invoiceDate, decimal invoiceAmount, string transactionType, int employeeId, int storeNumber, string paymentMethod, string poNumber, CancellationToken cancellationToken = default);
}
