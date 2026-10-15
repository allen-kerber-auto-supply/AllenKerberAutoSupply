using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreSalesRepository(
    ISalesRepDataRepository salesReps,
    ISalesCustomerDataRepository salesCustomers,
    ISalesCallDataRepository salesCalls) : ISalesRepository
{
    public Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default) =>
        salesReps.GetSalesRepListAsync(cancellationToken);

    public Task<bool> InsertSalesRepAsync(string repName, string repEmail, CancellationToken cancellationToken = default) =>
        salesReps.InsertSalesRepAsync(repName, repEmail, cancellationToken);

    public Task<bool> DeleteSalesRepAsync(string repEmail, CancellationToken cancellationToken = default) =>
        salesReps.DeleteSalesRepAsync(repEmail, cancellationToken);

    public Task<IReadOnlyList<SalesCustomer>> GetSalesCustomersAsync(string? salesRepEmail, CancellationToken cancellationToken = default) =>
        salesCustomers.GetSalesCustomersAsync(salesRepEmail, cancellationToken);

    public Task<IReadOnlyList<string>> GetCustomerListAsync(string? salesRepEmail, CancellationToken cancellationToken = default) =>
        salesCustomers.GetCustomerListAsync(salesRepEmail, cancellationToken);

    public Task<bool> InsertSalesCustomerAsync(string customerName, string contactName, string contactPhone, CancellationToken cancellationToken = default) =>
        salesCustomers.InsertSalesCustomerAsync(customerName, contactName, contactPhone, cancellationToken);

    public Task<bool> UpdateSalesCustomerAsync(int customerNumber, string customerName, string contactName, string contactPhone, CancellationToken cancellationToken = default) =>
        salesCustomers.UpdateSalesCustomerAsync(customerNumber, customerName, contactName, contactPhone, cancellationToken);

    public Task<bool> MergeSalesCustomersAsync(int survivingCustomerNumber, int duplicateCustomerNumber, CancellationToken cancellationToken = default) =>
        salesCustomers.MergeSalesCustomersAsync(survivingCustomerNumber, duplicateCustomerNumber, cancellationToken);

    public Task<bool> DeleteSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default) =>
        salesCustomers.DeleteSalesCustomerAsync(customerName, cancellationToken);

    public Task<bool> AssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default) =>
        salesCustomers.AssignAccountAsync(customerName, repEmail, cancellationToken);

    public Task<bool> UnAssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default) =>
        salesCustomers.UnAssignAccountAsync(customerName, repEmail, cancellationToken);

    public Task<SalesCall?> GetCallRecordAsync(int callId, CancellationToken cancellationToken = default) =>
        salesCalls.GetCallRecordAsync(callId, cancellationToken);

    public Task<IReadOnlyList<SalesCall>> GetCallRecordsAsync(string salesRepEmail, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default) =>
        salesCalls.GetCallRecordsAsync(salesRepEmail, fromDate, toDate, cancellationToken);

    public Task<IReadOnlyList<SalesCall>> GetCallRecordsForAccountAsync(string accountName, CancellationToken cancellationToken = default) =>
        salesCalls.GetCallRecordsForAccountAsync(accountName, cancellationToken);

    public Task<IReadOnlyList<SalesCall>> GetUpComingCallRecordsAsync(string salesRepEmail, CancellationToken cancellationToken = default) =>
        salesCalls.GetUpComingCallRecordsAsync(salesRepEmail, cancellationToken);

    public Task<IReadOnlyList<AccountCallsSummary>> GetCallsByAccountAsync(string salesRepEmail, CancellationToken cancellationToken = default) =>
        salesCalls.GetCallsByAccountAsync(salesRepEmail, cancellationToken);

    public Task<IReadOnlyList<AccountSummaryResponse>> GetAccountSummaryAsync(string salesRepEmail, CancellationToken cancellationToken = default) =>
        salesCalls.GetAccountSummaryAsync(salesRepEmail, cancellationToken);

    public Task<bool> InsertCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default) =>
        salesCalls.InsertCallRecordAsync(call, cancellationToken);

    public Task<bool> UpdateCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default) =>
        salesCalls.UpdateCallRecordAsync(call, cancellationToken);

    public Task<bool> DeleteCallRecordAsync(int callId, CancellationToken cancellationToken = default) =>
        salesCalls.DeleteCallRecordAsync(callId, cancellationToken);
}
