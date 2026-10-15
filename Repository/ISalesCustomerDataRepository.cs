using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface ISalesCustomerDataRepository
{
    Task<IReadOnlyList<SalesCustomer>> GetSalesCustomersAsync(string? salesRepEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCustomerListAsync(string? salesRepEmail, CancellationToken cancellationToken = default);
    Task<bool> InsertSalesCustomerAsync(string customerName, string contactName, string contactPhone, CancellationToken cancellationToken = default);
    Task<bool> UpdateSalesCustomerAsync(int customerNumber, string customerName, string contactName, string contactPhone, CancellationToken cancellationToken = default);
    Task<bool> MergeSalesCustomersAsync(int survivingCustomerNumber, int duplicateCustomerNumber, CancellationToken cancellationToken = default);
    Task<bool> DeleteSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default);
    Task<bool> AssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default);
    Task<bool> UnAssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default);
}
