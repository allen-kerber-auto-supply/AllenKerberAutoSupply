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
            else if (repsObj is string repText)
            {
                var cleaned = repText.Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    customer.AssignedSalesReps = [cleaned.ToLowerInvariant()];
                }
            }
            else if (repsObj is null)
            {
                customer.AssignedSalesReps = [];
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

    private async Task<List<SalesCall>> ApplyCustomerStatusAsync(IEnumerable<SalesCall> calls, CancellationToken cancellationToken)
    {
        var customerSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var customerNames = customerSnapshot.Documents
            .Select(document => MapSalesCustomer(document).CustomerName.Trim())
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
    public async Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("sales_reps")
            .WhereEqualTo(nameof(SalesRep.Status), "A")
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(MapSalesRep)
            .Where(r => !string.IsNullOrWhiteSpace(r.RepEmail))
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
        Query query = firestore.Collection("sales_customers");
        if (!string.IsNullOrWhiteSpace(salesRepEmail))
        {
            query = query.WhereArrayContains(nameof(SalesCustomer.AssignedSalesReps), salesRepEmail.Trim().ToLowerInvariant());
        }

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
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

        return customers
            .Where(c => string.IsNullOrWhiteSpace(salesRepEmail) ||
                        c.AssignedSalesReps.Any(r => string.Equals(r, salesRepEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
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

    public async Task<bool> ConvertProspectToCustomerAsync(int callId, CancellationToken cancellationToken = default)
    {
        var callDoc = await firestore.Collection("sales_calls").Document(callId.ToString()).GetSnapshotAsync(cancellationToken);
        if (!callDoc.Exists)
            return false;

        var call = MapSalesCall(callDoc);
        string accountName = (call.AccountName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(accountName))
            return false;

        var allCustomers = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        var customerDocument = allCustomers.Documents.FirstOrDefault(document =>
        {
            var customer = MapSalesCustomer(document);
            return string.Equals(customer.CustomerName, accountName, StringComparison.OrdinalIgnoreCase);
        });

        var salesRepEmail = (call.SalesRepEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (customerDocument is null)
        {
            int nextId = allCustomers.Documents.Count > 0
                ? allCustomers.Documents.Select(document => MapSalesCustomer(document).CustomerNumber).DefaultIfEmpty(0).Max() + 1
                : 1;
            var customer = new SalesCustomer
            {
                CustomerNumber = nextId,
                CustomerName = accountName,
                Guid = Guid.NewGuid().ToString(),
                AssignedSalesReps = string.IsNullOrWhiteSpace(salesRepEmail) ? [] : [salesRepEmail]
            };

            await firestore.Collection("sales_customers").Document(nextId.ToString())
                .SetAsync(customer, cancellationToken: cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(salesRepEmail))
        {
            var customer = MapSalesCustomer(customerDocument);
            if (!customer.AssignedSalesReps.Any(rep => string.Equals(rep, salesRepEmail, StringComparison.OrdinalIgnoreCase)))
            {
                customer.AssignedSalesReps.Add(salesRepEmail);
                await customerDocument.Reference.UpdateAsync(
                    nameof(SalesCustomer.AssignedSalesReps),
                    customer.AssignedSalesReps,
                    cancellationToken: cancellationToken);
            }
        }

        var callSnapshot = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);
        foreach (var document in callSnapshot.Documents)
        {
            var existingCall = MapSalesCall(document);
            if (!string.Equals(existingCall.AccountName?.Trim(), accountName, StringComparison.OrdinalIgnoreCase))
                continue;

            await document.Reference.UpdateAsync(nameof(SalesCall.IsProspect), false, cancellationToken: cancellationToken);
        }

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
        var existingDoc = allSnapshot.Documents.FirstOrDefault(d =>
        {
            var c = MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (existingDoc is not null)
        {
            var customer = MapSalesCustomer(existingDoc);
            if (customer.AssignedSalesReps.Count > 0)
                return false;

            if (!customer.AssignedSalesReps.Any(r => string.Equals(r, email, StringComparison.OrdinalIgnoreCase)))
            {
                customer.AssignedSalesReps.Add(email);
                await existingDoc.Reference.UpdateAsync(nameof(SalesCustomer.AssignedSalesReps), customer.AssignedSalesReps, cancellationToken: cancellationToken);
            }

            return true;
        }

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
        if (!doc.Exists)
            return null;

        var calls = await ApplyCustomerStatusAsync([MapSalesCall(doc)], cancellationToken);
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
            .Select(MapSalesCall)
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
            .Select(MapSalesCall)
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
            .Select(MapSalesCall)
            .OrderBy(c => c.CallDate)
            .ToList();

        var reps = (await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken))
            .Documents
            .Select(MapSalesRep)
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
            .Select(MapSalesCall)
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
            .Select(MapSalesCall)
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
            ? MapSalesCall(latestCallSnapshot.Documents[0]).CallID + 1
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
