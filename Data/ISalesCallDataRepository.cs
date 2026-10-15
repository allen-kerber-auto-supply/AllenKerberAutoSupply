using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface ISalesCallDataRepository
{
    Task<SalesCall?> GetCallRecordAsync(int callId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCall>> GetCallRecordsAsync(string salesRepEmail, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCall>> GetCallRecordsForAccountAsync(string accountName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesCall>> GetUpComingCallRecordsAsync(string salesRepEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountCallsSummary>> GetCallsByAccountAsync(string salesRepEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountSummaryResponse>> GetAccountSummaryAsync(string salesRepEmail, CancellationToken cancellationToken = default);
    Task<bool> InsertCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default);
    Task<bool> UpdateCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default);
    Task<bool> DeleteCallRecordAsync(int callId, CancellationToken cancellationToken = default);
}
