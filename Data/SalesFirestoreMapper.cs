using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

internal static class SalesFirestoreMapper
{
    public static SalesRep MapSalesRep(DocumentSnapshot doc)
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

    public static SalesCustomer MapSalesCustomer(DocumentSnapshot doc)
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

        if (data.TryGetValue("ContactName", out var contactNameObj) ||
            data.TryGetValue("contact_name", out contactNameObj) ||
            data.TryGetValue("Contact_Name", out contactNameObj))
        {
            customer.ContactName = contactNameObj?.ToString()?.Trim() ?? string.Empty;
        }

        if (data.TryGetValue("ContactPhone", out var contactPhoneObj) ||
            data.TryGetValue("contact_phone", out contactPhoneObj) ||
            data.TryGetValue("Contact_Phone", out contactPhoneObj) ||
            data.TryGetValue("Phone", out contactPhoneObj) ||
            data.TryGetValue("phone", out contactPhoneObj))
        {
            customer.ContactPhone = contactPhoneObj?.ToString()?.Trim() ?? string.Empty;
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

    public static SalesCall MapSalesCall(DocumentSnapshot doc)
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
}
