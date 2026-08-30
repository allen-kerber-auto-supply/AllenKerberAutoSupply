using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerSummary>> GetInvoiceCustomerListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCustomerEmailListAsync(int customerNumber, CancellationToken cancellationToken = default);
    Task<bool> InsertCustomerAsync(int customerNumber, string customerName, CancellationToken cancellationToken = default);
    Task<UserInfoResult?> GetUserInfoAsync(string userName, CancellationToken cancellationToken = default);
}
