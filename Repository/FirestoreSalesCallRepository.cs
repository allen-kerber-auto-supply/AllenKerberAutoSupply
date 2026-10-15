using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreSalesCallRepository(FirestoreDb firestore) : ISalesCallDataRepository
{
    private async Task<List<SalesCall>> ApplyCustomerStatusAsync(IEnumerable<SalesCall> calls, CancellationToken cancellationToken)
    {
        var customerSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var customerNames = customerSnapshot.Documents
            .Select(document => SalesFirestoreMapper.MapSalesCustomer(document).CustomerName.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = calls.ToList();
        foreach (var call in result)
        {
            call.IsProspect = !customerNames.Contains((call.AccountName ?? string.Empty).Trim());
        }

        return result;
    }

    // Sales Reps

    public async Task<SalesCall?> GetCallRecordAsync(int callId, CancellationToken cancellationToken = default)
    {
        var doc = await firestore.Collection("sales_calls").Document(callId.ToString()).GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return null;

        var calls = await ApplyCustomerStatusAsync([SalesFirestoreMapper.MapSalesCall(doc)], cancellationToken);
        return calls[0];
    }

    public async Task<IReadOnlyList<SalesCall>> GetCallRecordsAsync(string salesRepEmail, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        var start = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc));
        var requestedEnd = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var query = firestore.Collection("sales_calls")
            .WhereIn(nameof(SalesCall.Status), new[] { 1, 3 })
            .WhereGreaterThanOrEqualTo(nameof(SalesCall.CallDate), start)
            .WhereLessThanOrEqualTo(nameof(SalesCall.CallDate), requestedEnd);
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.WhereEqualTo(nameof(SalesCall.SalesRepEmail), email);
        }

        var snapshot = await query.GetSnapshotAsync(cancellationToken);

        var calls = snapshot.Documents
            .Select(SalesFirestoreMapper.MapSalesCall)
            .OrderByDescending(c => c.CallDate)
            .ToList();

        return await ApplyCustomerStatusAsync(calls, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesCall>> GetCallRecordsForAccountAsync(string accountName, CancellationToken cancellationToken = default)
    {
        string name = (accountName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return [];

        var snapshot = await firestore.Collection("sales_calls")
            .WhereEqualTo(nameof(SalesCall.AccountName), name)
            .GetSnapshotAsync(cancellationToken);

        var calls = snapshot.Documents
            .Select(SalesFirestoreMapper.MapSalesCall)
            .Where(c => string.Equals((c.AccountName ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.CallDate ?? c.CreatedDate)
            .ToList();

        return await ApplyCustomerStatusAsync(calls, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesCall>> GetUpComingCallRecordsAsync(string salesRepEmail, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        var query = firestore.Collection("sales_calls")
            .WhereIn(nameof(SalesCall.Status), new[] { 0, 2 });
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.WhereEqualTo(nameof(SalesCall.SalesRepEmail), email);
        }

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var calls = snapshot.Documents
            .Select(SalesFirestoreMapper.MapSalesCall)
            .OrderBy(c => c.CallDate)
            .ToList();

        var reps = (await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken))
            .Documents
            .Select(SalesFirestoreMapper.MapSalesRep)
            .ToList();
        foreach (var call in calls)
        {
            var rep = reps.FirstOrDefault(r =>
                (!string.IsNullOrWhiteSpace(call.SalesRepEmail) &&
                 string.Equals(r.RepEmail, call.SalesRepEmail, StringComparison.OrdinalIgnoreCase)) ||
                (call.SalesRepId != 0 && r.Id == call.SalesRepId));
            call.RepName = rep?.RepName ?? string.Empty;
        }

        return await ApplyCustomerStatusAsync(calls, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountCallsSummary>> GetCallsByAccountAsync(string salesRepEmail, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();

        var query = firestore.Collection("sales_calls")
            .WhereEqualTo(nameof(SalesCall.Status), 1);
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.WhereEqualTo(nameof(SalesCall.SalesRepEmail), email);
        }

        var snapshot = await query.GetSnapshotAsync(cancellationToken);

        var calls = snapshot.Documents
            .Select(SalesFirestoreMapper.MapSalesCall)
            .ToList();
        calls = await ApplyCustomerStatusAsync(calls, cancellationToken);

        return calls
            .GroupBy(c => new { c.AccountName, c.IsProspect })
            .Select(g => new AccountCallsSummary
            {
                AccountName = g.Key.AccountName,
                IsProspect = g.Key.IsProspect,
                CallCount = g.Count(),
                LatestCall = g.Max(c => c.CallDate?.ToDateTime())
            })
            .OrderBy(a => a.AccountName)
            .ToList();
    }

    public async Task<IReadOnlyList<AccountSummaryResponse>> GetAccountSummaryAsync(string salesRepEmail, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();

        Query query = firestore.Collection("sales_calls")
            .WhereIn(nameof(SalesCall.Status), new[] { 1, 3 });
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.WhereEqualTo(nameof(SalesCall.SalesRepEmail), email);
        }

        var snapshot = await query.GetSnapshotAsync(cancellationToken);

        var calls = snapshot.Documents
            .Select(SalesFirestoreMapper.MapSalesCall)
            .OrderByDescending(c => c.CallDate ?? c.CreatedDate)
            .ToList();
        calls = await ApplyCustomerStatusAsync(calls, cancellationToken);

        return calls
            .GroupBy(c => c.AccountName)
            .Select(g => new AccountSummaryResponse
            {
                AccountName = g.Key,
                Calls = g.OrderByDescending(c => c.CallDate ?? c.CreatedDate).ToList(),
                TotalCalls = g.Count(),
                CompletedCalls = g.Count(c => c.Status == 1),
                ScheduledCalls = g.Count(c => c.Status == 0),
                LastCallDate = g.Max(c => c.CallDate?.ToDateTime() ?? c.CreatedDate?.ToDateTime())
            })
            .OrderBy(a => a.AccountName)
            .ToList();
    }

    public async Task<bool> InsertCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default)
    {
        string accountName = (call.AccountName ?? string.Empty).Trim();
        var latestCallSnapshot = await firestore.Collection("sales_calls")
            .OrderByDescending(nameof(SalesCall.CallID))
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);
        int nextCallId = latestCallSnapshot.Documents.Count > 0
            ? SalesFirestoreMapper.MapSalesCall(latestCallSnapshot.Documents[0]).CallID + 1
            : 1;

        // Check if account name exists in sales_customers to set IsProspect
        var customerSnapshot = await firestore.Collection("sales_customers")
            .WhereEqualTo(nameof(SalesCustomer.CustomerName), accountName)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);
        bool customerExists = customerSnapshot.Documents.Count > 0;

        call.CallID = nextCallId;
        call.CreatedDate ??= Timestamp.GetCurrentTimestamp();
        call.IsProspect = !customerExists;

        var docRef = firestore.Collection("sales_calls").Document(nextCallId.ToString());
        await docRef.SetAsync(call, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> UpdateCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default)
    {
        var docRef = firestore.Collection("sales_calls").Document(call.CallID.ToString());
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return false;

        string accountName = (call.AccountName ?? string.Empty).Trim();
        var allCust = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        bool customerExists = allCust.Documents.Any(d =>
        {
            var c = SalesFirestoreMapper.MapSalesCustomer(d);
            return string.Equals(c.CustomerName, accountName, StringComparison.OrdinalIgnoreCase);
        });
        call.IsProspect = !customerExists;

        await docRef.SetAsync(call, SetOptions.MergeAll, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCallRecordAsync(int callId, CancellationToken cancellationToken = default)
    {
        var docRef = firestore.Collection("sales_calls").Document(callId.ToString());
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return false;

        await docRef.DeleteAsync(cancellationToken: cancellationToken);
        return true;
    }
}
