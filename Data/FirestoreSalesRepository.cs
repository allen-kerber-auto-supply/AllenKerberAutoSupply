using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreSalesRepository(FirestoreDb firestore) : ISalesRepository
{
    private static SalesRep MapSalesRep(DocumentSnapshot doc)
    {
        var data = doc.ToDictionary();
        var rep = new SalesRep();

        if (data.TryGetValue("Id", out var idObj) ||
            data.TryGetValue("id", out idObj))
        {
            if (idObj is long l) rep.Id = (int)l;
            else if (idObj is int i) rep.Id = i;
            else if (int.TryParse(idObj?.ToString(), out var parsed)) rep.Id = parsed;
        }

        if (data.TryGetValue("RepName", out var nameObj) ||
            data.TryGetValue("rep_name", out nameObj) ||
            data.TryGetValue("Rep_Name", out nameObj) ||
            data.TryGetValue("Name", out nameObj) ||
            data.TryGetValue("name", out nameObj))
        {
            rep.RepName = nameObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("RepEmail", out var emailObj) ||
            data.TryGetValue("rep_email", out emailObj) ||
            data.TryGetValue("Rep_Email", out emailObj) ||
            data.TryGetValue("Email", out emailObj) ||
            data.TryGetValue("email", out emailObj))
        {
            rep.RepEmail = emailObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(rep.RepEmail) && doc.Id.Contains('@'))
        {
            rep.RepEmail = doc.Id.Trim();
        }

        if (data.TryGetValue("Status", out var statusObj) ||
            data.TryGetValue("status", out statusObj))
        {
            rep.Status = statusObj?.ToString()?.Trim() ?? "A";
        }

        return rep;
    }

    private static SalesCustomer MapSalesCustomer(DocumentSnapshot doc)
    {
        var data = doc.ToDictionary();
        var customer = new SalesCustomer();

        if (data.TryGetValue("CustomerNumber", out var numObj) ||
            data.TryGetValue("customer_no", out numObj) ||
            data.TryGetValue("customer_number", out numObj) ||
            data.TryGetValue("Customer_No", out numObj) ||
            data.TryGetValue("id", out numObj) ||
            data.TryGetValue("Id", out numObj))
        {
            if (numObj is long l) customer.CustomerNumber = (int)l;
            else if (numObj is int i) customer.CustomerNumber = i;
            else if (int.TryParse(numObj?.ToString(), out var parsedNum)) customer.CustomerNumber = parsedNum;
        }
        if (customer.CustomerNumber == 0 && int.TryParse(doc.Id, out var docIdNum))
        {
            customer.CustomerNumber = docIdNum;
        }

        if (data.TryGetValue("CustomerName", out var nameObj) ||
            data.TryGetValue("customer_name", out nameObj) ||
            data.TryGetValue("Customer_Name", out nameObj) ||
            data.TryGetValue("AccountName", out nameObj) ||
            data.TryGetValue("account_name", out nameObj) ||
            data.TryGetValue("Name", out nameObj) ||
            data.TryGetValue("name", out nameObj))
        {
            customer.CustomerName = nameObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("Guid", out var guidObj) ||
            data.TryGetValue("guid", out guidObj))
        {
            customer.Guid = guidObj?.ToString() ?? string.Empty;
        }

        if (data.TryGetValue("AssignedSalesReps", out var repsObj) ||
            data.TryGetValue("assigned_sales_reps", out repsObj) ||
            data.TryGetValue("assignedSalesReps", out repsObj) ||
            data.TryGetValue("AssignedReps", out repsObj) ||
            data.TryGetValue("assigned_reps", out repsObj) ||
            data.TryGetValue("SalesReps", out repsObj) ||
            data.TryGetValue("sales_reps", out repsObj))
        {
            if (repsObj is IEnumerable<object> list)
            {
                customer.AssignedSalesReps = list
                    .Select(o => o?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct()
                    .ToList();
            }
        }
        else if (data.TryGetValue("SalesRep", out var singleRepObj) ||
                 data.TryGetValue("sales_rep", out singleRepObj) ||
                 data.TryGetValue("RepEmail", out singleRepObj) ||
                 data.TryGetValue("rep_email", out singleRepObj))
        {
            var repStr = singleRepObj?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(repStr))
            {
                customer.AssignedSalesReps = [repStr];
            }
        }

        return customer;
    }

    private static SalesCall MapSalesCall(DocumentSnapshot doc)
    {
        var data = doc.ToDictionary();
        var call = new SalesCall();

        if (data.TryGetValue("CallID", out var idObj) ||
            data.TryGetValue("call_id", out idObj) ||
            data.TryGetValue("Call_Id", out idObj) ||
            data.TryGetValue("Id", out idObj) ||
            data.TryGetValue("id", out idObj))
        {
            if (idObj is long l) call.CallID = (int)l;
            else if (idObj is int i) call.CallID = i;
            else if (int.TryParse(idObj?.ToString(), out var parsed)) call.CallID = parsed;
        }
        if (call.CallID == 0 && int.TryParse(doc.Id, out var docCallId))
        {
            call.CallID = docCallId;
        }

        if (data.TryGetValue("AccountName", out var accObj) ||
            data.TryGetValue("account_name", out accObj) ||
            data.TryGetValue("Account_Name", out accObj) ||
            data.TryGetValue("CustomerName", out accObj) ||
            data.TryGetValue("customer_name", out accObj))
        {
            call.AccountName = accObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("ContactName", out var contactObj) ||
            data.TryGetValue("contact_name", out contactObj) ||
            data.TryGetValue("Contact_Name", out contactObj))
        {
            call.ContactName = contactObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("ContactPhone", out var phoneObj) ||
            data.TryGetValue("contact_phone", out phoneObj) ||
            data.TryGetValue("Contact_Phone", out phoneObj) ||
            data.TryGetValue("Phone", out phoneObj) ||
            data.TryGetValue("phone", out phoneObj))
        {
            call.ContactPhone = phoneObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("Comments", out var commentObj) ||
            data.TryGetValue("comments", out commentObj))
        {
            call.Comments = commentObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("CallDuration", out var durObj) ||
            data.TryGetValue("call_duration", out durObj) ||
            data.TryGetValue("Call_Duration", out durObj))
        {
            if (durObj is long l) call.CallDuration = (int)l;
            else if (durObj is int i) call.CallDuration = i;
            else if (int.TryParse(durObj?.ToString(), out var parsed)) call.CallDuration = parsed;
        }

        if (data.TryGetValue("SalesRepId", out var repIdObj) ||
            data.TryGetValue("sales_rep_id", out repIdObj) ||
            data.TryGetValue("SalesRep", out repIdObj) ||
            data.TryGetValue("sales_rep", out repIdObj))
        {
            if (repIdObj is long l) call.SalesRepId = (int)l;
            else if (repIdObj is int i) call.SalesRepId = i;
            else if (int.TryParse(repIdObj?.ToString(), out var parsed)) call.SalesRepId = parsed;
        }

        if (data.TryGetValue("SalesRepEmail", out var repEmailObj) ||
            data.TryGetValue("sales_rep_email", out repEmailObj) ||
            data.TryGetValue("RepEmail", out repEmailObj) ||
            data.TryGetValue("rep_email", out repEmailObj))
        {
            call.SalesRepEmail = repEmailObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("Status", out var stObj) ||
            data.TryGetValue("status", out stObj))
        {
            if (stObj is long l) call.Status = (int)l;
            else if (stObj is int i) call.Status = i;
            else if (int.TryParse(stObj?.ToString(), out var parsed)) call.Status = parsed;
        }

        if (data.TryGetValue("IsProspect", out var prospObj) ||
            data.TryGetValue("is_prospect", out prospObj) ||
            data.TryGetValue("Is_Prospect", out prospObj))
        {
            if (prospObj is bool b) call.IsProspect = b;
            else if (bool.TryParse(prospObj?.ToString(), out var parsed)) call.IsProspect = parsed;
        }

        if (data.TryGetValue("CallDate", out var callDateObj) ||
            data.TryGetValue("call_date", out callDateObj) ||
            data.TryGetValue("Call_DTM", out callDateObj))
        {
            if (callDateObj is Timestamp ts) call.CallDate = ts;
            else if (callDateObj is DateTime dt) call.CallDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }

        if (data.TryGetValue("CreatedDate", out var crDateObj) ||
            data.TryGetValue("created_date", out crDateObj) ||
            data.TryGetValue("Created_DTM", out crDateObj))
        {
            if (crDateObj is Timestamp ts) call.CreatedDate = ts;
            else if (crDateObj is DateTime dt) call.CreatedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }

        if (data.TryGetValue("FollowUpDate", out var fuDateObj) ||
            data.TryGetValue("follow_up_date", out fuDateObj) ||
            data.TryGetValue("FollowUp_DTM", out fuDateObj))
        {
            if (fuDateObj is Timestamp ts) call.FollowUpDate = ts;
            else if (fuDateObj is DateTime dt) call.FollowUpDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }

        return call;
    }

    // Sales Reps
    public async Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(MapSalesRep)
            .Where(r => string.Equals(r.Status, "A", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.RepEmail))
            .OrderBy(r => r.RepName)
            .ToList();
    }

    public async Task<bool> InsertSalesRepAsync(string repName, string repEmail, CancellationToken cancellationToken = default)
    {
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var allSnapshot = await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken);
        bool exists = allSnapshot.Documents.Any(d =>
        {
            var r = MapSalesRep(d);
            return string.Equals(r.RepEmail, email, StringComparison.OrdinalIgnoreCase);
        });

        if (exists)
            return false;

        int nextId = allSnapshot.Documents.Count > 0
            ? allSnapshot.Documents.Select(d => MapSalesRep(d).Id).DefaultIfEmpty(0).Max() + 1
            : 1;

        var rep = new SalesRep
        {
            Id = nextId,
            RepName = (repName ?? string.Empty).Trim(),
            RepEmail = email,
            Status = "A"
        };

        var docRef = firestore.Collection("sales_reps").Document(email);
        await docRef.SetAsync(rep, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSalesRepAsync(string repEmail, CancellationToken cancellationToken = default)
    {
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        var allSnapshot = await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken);
        var matching = allSnapshot.Documents.Where(d =>
        {
            var r = MapSalesRep(d);
            return string.Equals(r.RepEmail, email, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (matching.Count == 0)
            return false;

        foreach (var doc in matching)
        {
            await doc.Reference.UpdateAsync(nameof(SalesRep.Status), "T", cancellationToken: cancellationToken);
        }

        return true;
    }

    // Sales Customers & Account Assignments
    public async Task<IReadOnlyList<SalesCustomer>> GetSalesCustomersAsync(string? salesRepEmail, CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        List<SalesCustomer> customers;

        if (snapshot.Documents.Count > 0)
        {
            customers = snapshot.Documents
                .Select(MapSalesCustomer)
                .Where(c => !string.IsNullOrWhiteSpace(c.CustomerName))
                .ToList();
        }
        else
        {
            // Fallback to customers collection if sales_customers is empty
            var custSnapshot = await firestore.Collection("customers").GetSnapshotAsync(cancellationToken);
            customers = custSnapshot.Documents
                .Select(d =>
                {
                    var c = d.ConvertTo<Customer>();
                    return new SalesCustomer
                    {
                        CustomerNumber = c.CustomerNumber,
                        CustomerName = c.CustomerName,
                        AssignedSalesReps = []
                    };
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.CustomerName))
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(salesRepEmail))
        {
            return customers
                .OrderBy(c => c.CustomerName)
                .ToList();
        }

        string email = salesRepEmail.Trim().ToLowerInvariant();
        return customers
            .Where(c => c.AssignedSalesReps.Any(r => string.Equals(r, email, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.CustomerName)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetCustomerListAsync(string? salesRepEmail, CancellationToken cancellationToken = default)
    {
        var customers = await GetSalesCustomersAsync(salesRepEmail, cancellationToken);
        return customers
            .Select(c => c.CustomerName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();
    }

    public async Task<bool> InsertSalesCustomerAsync(string customerName, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var allSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        bool exists = allSnapshot.Documents.Any(d =>
        {
            var c = MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (exists)
            return false;

        int nextId = allSnapshot.Documents.Count > 0
            ? allSnapshot.Documents.Select(d => MapSalesCustomer(d).CustomerNumber).DefaultIfEmpty(0).Max() + 1
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
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var allSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var matching = allSnapshot.Documents.Where(d =>
        {
            var c = MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (matching.Count == 0)
            return false;

        foreach (var doc in matching)
        {
            await doc.Reference.DeleteAsync(cancellationToken: cancellationToken);
        }

        return true;
    }

    public async Task<bool> AssignAccountAsync(string customerName, string repEmail, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            return false;

        var allSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var doc = allSnapshot.Documents.FirstOrDefault(d =>
        {
            var c = MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (doc is null)
        {
            // Seed from customers collection or create new
            var custQuery = await firestore.Collection("customers")
                .WhereEqualTo(nameof(Customer.CustomerName), name)
                .Limit(1)
                .GetSnapshotAsync(cancellationToken);

            int custNo = 0;
            if (custQuery.Documents.Count > 0)
            {
                var cust = custQuery.Documents[0].ConvertTo<Customer>();
                custNo = cust.CustomerNumber;
            }

            if (custNo == 0)
            {
                custNo = allSnapshot.Documents.Count > 0
                    ? allSnapshot.Documents.Select(d => MapSalesCustomer(d).CustomerNumber).DefaultIfEmpty(0).Max() + 1
                    : 1;
            }

            var newDocRef = firestore.Collection("sales_customers").Document(custNo.ToString());
            var newCustomer = new SalesCustomer
            {
                CustomerNumber = custNo,
                CustomerName = name,
                Guid = Guid.NewGuid().ToString(),
                AssignedSalesReps = [email]
            };
            await newDocRef.SetAsync(newCustomer, cancellationToken: cancellationToken);
            return true;
        }

        var customer = MapSalesCustomer(doc);
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
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            return false;

        var allSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var doc = allSnapshot.Documents.FirstOrDefault(d =>
        {
            var c = MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (doc is null)
            return false;

        var customer = MapSalesCustomer(doc);
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
        return doc.Exists ? MapSalesCall(doc) : null;
    }

    public async Task<IReadOnlyList<SalesCall>> GetCallRecordsAsync(string salesRepEmail, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        var start = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc));
        var end = Timestamp.FromDateTime(DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));

        var snapshot = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(MapSalesCall)
            .Where(c => c.Status == 1)
            .Where(c => string.IsNullOrWhiteSpace(email) || string.Equals(c.SalesRepEmail, email, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.CallDate >= start && c.CallDate <= end)
            .OrderByDescending(c => c.CallDate)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesCall>> GetCallRecordsForAccountAsync(string accountName, CancellationToken cancellationToken = default)
    {
        string name = (accountName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return [];

        var snapshot = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(MapSalesCall)
            .Where(c => string.Equals((c.AccountName ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.CallDate ?? c.CreatedDate)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesCall>> GetUpComingCallRecordsAsync(string salesRepEmail, DateTime currentDateTime, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        var endOfFromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(fromDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));

        var snapshot = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);

        var calls = snapshot.Documents
            .Select(MapSalesCall)
            .Where(c => c.Status == 0)
            .Where(c => string.IsNullOrWhiteSpace(email) || string.Equals(c.SalesRepEmail, email, StringComparison.OrdinalIgnoreCase))
            .Where(c => (c.CallDate.HasValue && c.CallDate <= endOfFromDate) || (c.FollowUpDate.HasValue && c.FollowUpDate <= endOfFromDate))
            .OrderBy(c => c.CallDate ?? c.FollowUpDate)
            .ToList();

        return calls;
    }

    public async Task<IReadOnlyList<AccountCallsSummary>> GetCallsByAccountAsync(string salesRepEmail, CancellationToken cancellationToken = default)
    {
        string email = (salesRepEmail ?? string.Empty).Trim().ToLowerInvariant();

        var snapshot = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(MapSalesCall)
            .Where(c => c.Status == 1)
            .Where(c => string.IsNullOrWhiteSpace(email) || string.Equals(c.SalesRepEmail, email, StringComparison.OrdinalIgnoreCase))
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
        var allCalls = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);
        int nextCallId = allCalls.Documents.Count > 0
            ? allCalls.Documents.Select(d => MapSalesCall(d).CallID).DefaultIfEmpty(0).Max() + 1
            : 1;

        // Check if account name exists in sales_customers to set IsProspect
        var allCust = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        bool customerExists = allCust.Documents.Any(d =>
        {
            var c = MapSalesCustomer(d);
            return string.Equals(c.CustomerName, accountName, StringComparison.OrdinalIgnoreCase);
        });

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
            var c = MapSalesCustomer(d);
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
