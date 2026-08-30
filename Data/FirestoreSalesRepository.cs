using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreSalesRepository(FirestoreDb firestore) : ISalesRepository
{
    // Sales Reps
    public async Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("sales_reps")
            .WhereEqualTo(nameof(SalesRep.Status), "A")
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(d => d.ConvertTo<SalesRep>())
            .OrderBy(r => r.RepName)
            .ToList();
    }

    public async Task<bool> InsertSalesRepAsync(string repName, string repEmail, CancellationToken cancellationToken = default)
    {
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var docRef = firestore.Collection("sales_reps").Document(email);
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (doc.Exists)
            return false;

        var repsSnapshot = await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken);
        int nextId = repsSnapshot.Documents.Count > 0
            ? repsSnapshot.Documents.Select(d => d.ConvertTo<SalesRep>().Id).DefaultIfEmpty(0).Max() + 1
            : 1;

        var rep = new SalesRep
        {
            Id = nextId,
            RepName = (repName ?? string.Empty).Trim(),
            RepEmail = (repEmail ?? string.Empty).Trim(),
            Status = "A"
        };

        await docRef.SetAsync(rep, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSalesRepAsync(string repEmail, CancellationToken cancellationToken = default)
    {
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        var docRef = firestore.Collection("sales_reps").Document(email);
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return false;

        await docRef.UpdateAsync(nameof(SalesRep.Status), "T", cancellationToken: cancellationToken);
        return true;
    }

    // Sales Customers & Account Assignments
    public async Task<IReadOnlyList<string>> GetCustomerListAsync(string? salesRepEmail, CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var customers = snapshot.Documents.Select(d => d.ConvertTo<SalesCustomer>()).ToList();

        if (string.IsNullOrWhiteSpace(salesRepEmail))
        {
            return customers
                .Select(c => c.CustomerName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name)
                .ToList();
        }

        string email = salesRepEmail.Trim().ToLowerInvariant();
        return customers
            .Where(c => c.AssignedSalesReps.Any(r => string.Equals(r, email, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.CustomerName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name)
            .ToList();
    }

    public async Task<bool> InsertSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var query = await firestore.Collection("sales_customers")
            .WhereEqualTo(nameof(SalesCustomer.CustomerName), name)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        if (query.Documents.Count > 0)
            return false;

        var allSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        int nextId = allSnapshot.Documents.Count > 0
            ? allSnapshot.Documents.Select(d => d.ConvertTo<SalesCustomer>().CustomerNumber).DefaultIfEmpty(0).Max() + 1
            : 1;

        var docRef = firestore.Collection("sales_customers").Document(nextId.ToString());
        var customer = new SalesCustomer
        {
            CustomerNumber = nextId,
            CustomerName = name,
            Guid = Guid.NewGuid().ToString(),
            AssignedSalesReps = []
        };

        await docRef.SetAsync(customer, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        var query = await firestore.Collection("sales_customers")
            .WhereEqualTo(nameof(SalesCustomer.CustomerName), name)
            .GetSnapshotAsync(cancellationToken);

        if (query.Documents.Count == 0)
            return false;

        foreach (var doc in query.Documents)
        {
            await doc.Reference.DeleteAsync(cancellationToken: cancellationToken);
        }

        return true;
    }

    public async Task<bool> AssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        string email = (repEmail ?? string.Empty).Trim();

        var query = await firestore.Collection("sales_customers")
            .WhereEqualTo(nameof(SalesCustomer.CustomerName), name)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        if (query.Documents.Count == 0)
            return false;

        var doc = query.Documents[0];
        var customer = doc.ConvertTo<SalesCustomer>();

        if (!customer.AssignedSalesReps.Any(r => string.Equals(r, email, StringComparison.OrdinalIgnoreCase)))
        {
            customer.AssignedSalesReps.Add(email);
            await doc.Reference.UpdateAsync(nameof(SalesCustomer.AssignedSalesReps), customer.AssignedSalesReps, cancellationToken: cancellationToken);
        }

        return true;
    }

    public async Task<bool> UnAssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        string email = (repEmail ?? string.Empty).Trim();

        var query = await firestore.Collection("sales_customers")
            .WhereEqualTo(nameof(SalesCustomer.CustomerName), name)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        if (query.Documents.Count == 0)
            return false;

        var doc = query.Documents[0];
        var customer = doc.ConvertTo<SalesCustomer>();

        var updatedReps = customer.AssignedSalesReps
            .Where(r => !string.Equals(r, email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await doc.Reference.UpdateAsync(nameof(SalesCustomer.AssignedSalesReps), updatedReps, cancellationToken: cancellationToken);
        return true;
    }

    // Sales Calls
    public async Task<SalesCall?> GetCallRecordAsync(int callId, CancellationToken cancellationToken = default)
    {
        var doc = await firestore.Collection("sales_calls").Document(callId.ToString()).GetSnapshotAsync(cancellationToken);
        return doc.Exists ? doc.ConvertTo<SalesCall>() : null;
    }

    public async Task<IReadOnlyList<SalesCall>> GetCallRecordsAsync(string salesRepEmail, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        var start = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        var end = Timestamp.FromDateTime(DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Utc));

        var snapshot = await firestore.Collection("sales_calls")
            .WhereEqualTo(nameof(SalesCall.Status), 1)
            .WhereGreaterThanOrEqualTo(nameof(SalesCall.CallDate), start)
            .WhereLessThanOrEqualTo(nameof(SalesCall.CallDate), end)
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(d => d.ConvertTo<SalesCall>())
            .Where(c => string.Equals(c.SalesRepEmail, email, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.CallDate)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesCall>> GetUpComingCallRecordsAsync(string salesRepEmail, DateTime currentDateTime, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        var fromTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        var toTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate.AddDays(1), DateTimeKind.Utc));
        var nowTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));

        var snapshot = await firestore.Collection("sales_calls")
            .WhereEqualTo(nameof(SalesCall.Status), 0)
            .GetSnapshotAsync(cancellationToken);

        var calls = snapshot.Documents
            .Select(d => d.ConvertTo<SalesCall>())
            .Where(c => string.Equals(c.SalesRepEmail, email, StringComparison.OrdinalIgnoreCase))
            .Where(c =>
            {
                bool isPastDue = c.CallDate < nowTimestamp && (c.FollowUpDate == null || c.FollowUpDate < nowTimestamp);
                bool inCallDateRange = c.CallDate >= fromTimestamp && c.CallDate <= toTimestamp;
                bool inFollowUpRange = c.FollowUpDate >= fromTimestamp && c.FollowUpDate <= toTimestamp;
                return isPastDue || inCallDateRange || inFollowUpRange;
            })
            .OrderBy(c => c.FollowUpDate ?? c.CallDate)
            .ToList();

        return calls;
    }

    public async Task<IReadOnlyList<AccountCallsSummary>> GetCallsByAccountAsync(string salesRepEmail, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();

        var snapshot = await firestore.Collection("sales_calls")
            .WhereEqualTo(nameof(SalesCall.Status), 1)
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(d => d.ConvertTo<SalesCall>())
            .Where(c => string.Equals(c.SalesRepEmail, email, StringComparison.OrdinalIgnoreCase))
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

    public async Task<bool> InsertCallRecordAsync(SalesCall call, CancellationToken cancellationToken = default)
    {
        string accountName = (call.AccountName ?? string.Empty).Trim();
        var query = await firestore.Collection("sales_calls")
            .WhereEqualTo(nameof(SalesCall.AccountName), accountName)
            .WhereEqualTo(nameof(SalesCall.CallDate), call.CallDate)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        if (query.Documents.Count > 0)
            return false;

        var allCalls = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);
        int nextCallId = allCalls.Documents.Count > 0
            ? allCalls.Documents.Select(d => d.ConvertTo<SalesCall>().CallID).DefaultIfEmpty(0).Max() + 1
            : 1;

        // Check if account name exists in sales_customers to set IsProspect
        var custQuery = await firestore.Collection("sales_customers")
            .WhereEqualTo(nameof(SalesCustomer.CustomerName), accountName)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        call.CallID = nextCallId;
        call.CreatedDate ??= Timestamp.GetCurrentTimestamp();
        call.Status = 0;
        call.IsProspect = custQuery.Documents.Count == 0;

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

        await docRef.SetAsync(call, SetOptions.MergeAll, cancellationToken: cancellationToken);
        return true;
    }
}
