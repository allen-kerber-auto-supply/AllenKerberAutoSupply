using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface ISalesRepository
{
    // Sales Reps
    Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default);
    Task<bool> InsertSalesRepAsync(string repName, string repEmail, CancellationToken cancellationToken = default);
    Task<bool> DeleteSalesRepAsync(string repEmail, CancellationToken cancellationToken = default);

    // Sales Customers & Account Assignments
    Task<IReadOnlyList<SalesCustomer>> GetSalesCustomersAsync(string? salesRepEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCustomerListAsync(string? salesRepEmail, CancellationToken cancellationToken = default);
    Task<bool> InsertSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default);
    Task<bool> DeleteSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default);
    Task<bool> AssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default);
    Task<bool> UnAssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default);

    // Sales Calls
    Task<SalesCall?> GetCallRecordAsync(int callId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCall>> GetCallRecordsAsync(string salesRepEmail, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCall>> GetCallRecordsForAccountAsync(string accountName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCall>> GetUpComingCallRecordsAsync(string salesRepEmail, DateTime currentDateTime, DateTime fromDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountCallsSummary>> GetCallsByAccountAsync(string salesRepEmail, CancellationToken cancellationToken = default);
    Task<bool> InsertCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default);
    Task<bool> UpdateCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default);
    Task<bool> DeleteCallRecordAsync(int callId, CancellationToken cancellationToken = default);
}
