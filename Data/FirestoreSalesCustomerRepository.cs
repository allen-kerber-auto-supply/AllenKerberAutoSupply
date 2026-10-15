using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreSalesCustomerRepository(FirestoreDb firestore, ICustomerListCache customerListCache) : ISalesCustomerDataRepository
{
    public async Task<IReadOnlyList<SalesCustomer>> GetSalesCustomersAsync(string? salesRepEmail, CancellationToken cancellationToken = default)
    {
        var customers = await customerListCache.GetSalesCustomerListAsync(LoadSalesCustomersAsync, cancellationToken);
        return customers
            .Where(c => string.IsNullOrWhiteSpace(salesRepEmail) ||
                        c.AssignedSalesReps.Any(r => string.Equals(r, salesRepEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.CustomerName)
            .ToList();
    }

    private async Task<IReadOnlyList<SalesCustomer>> LoadSalesCustomersAsync(CancellationToken cancellationToken)
    {
        Query query = firestore.Collection("sales_customers");

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        List<SalesCustomer> customers;

        if (snapshot.Documents.Count > 0)
        {
            customers = snapshot.Documents
                .Select(SalesFirestoreMapper.MapSalesCustomer)
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
                    var c = d.ConvertTo<FirestoreCustomer>();
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

        return customers;
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

    public async Task<bool> InsertSalesCustomerAsync(string customerName, string contactName, string contactPhone, CancellationToken cancellationToken = default)
    {
        string name = (customerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var allSnapshot = await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken);
        bool exists = allSnapshot.Documents.Any(d =>
        {
            var c = SalesFirestoreMapper.MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (exists)
            return false;

        int nextId = allSnapshot.Documents.Count > 0
            ? allSnapshot.Documents.Select(d => SalesFirestoreMapper.MapSalesCustomer(d).CustomerNumber).DefaultIfEmpty(0).Max() + 1
            : 1;

        var docRef = firestore.Collection("sales_customers").Document(nextId.ToString());
        var customer = new SalesCustomer
        {
            CustomerNumber = nextId,
            CustomerName = name,
            ContactName = (contactName ?? string.Empty).Trim(),
            ContactPhone = (contactPhone ?? string.Empty).Trim(),
            Guid = Guid.NewGuid().ToString(),
            AssignedSalesReps = []
        };

        await docRef.SetAsync(customer, cancellationToken: cancellationToken);
        customerListCache.InvalidateSalesCustomers();
        return true;
    }

    public async Task<bool> UpdateSalesCustomerAsync(int customerNumber, string customerName, string contactName, string contactPhone, CancellationToken cancellationToken = default)
    {
        if (customerNumber <= 0 || string.IsNullOrWhiteSpace(customerName))
            return false;

        var doc = await firestore.Collection("sales_customers").Document(customerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return false;

        var duplicate = (await firestore.Collection("sales_customers").GetSnapshotAsync(cancellationToken)).Documents.Any(other =>
            other.Id != doc.Id && string.Equals(SalesFirestoreMapper.MapSalesCustomer(other).CustomerName, customerName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            return false;

        await doc.Reference.UpdateAsync(new Dictionary<string, object>
        {
            [nameof(SalesCustomer.CustomerName)] = customerName.Trim(),
            [nameof(SalesCustomer.ContactName)] = (contactName ?? string.Empty).Trim(),
            [nameof(SalesCustomer.ContactPhone)] = (contactPhone ?? string.Empty).Trim()
        }, cancellationToken: cancellationToken);
        customerListCache.InvalidateSalesCustomers();
        return true;
    }

    public async Task<bool> MergeSalesCustomersAsync(int survivingCustomerNumber, int duplicateCustomerNumber, CancellationToken cancellationToken = default)
    {
        if (survivingCustomerNumber <= 0 || duplicateCustomerNumber <= 0 || survivingCustomerNumber == duplicateCustomerNumber)
            return false;

        var collection = firestore.Collection("sales_customers");
        var primaryDoc = await collection.Document(survivingCustomerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        var duplicateDoc = await collection.Document(duplicateCustomerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        if (!primaryDoc.Exists || !duplicateDoc.Exists)
            return false;

        var primary = SalesFirestoreMapper.MapSalesCustomer(primaryDoc);
        var duplicate = SalesFirestoreMapper.MapSalesCustomer(duplicateDoc);
        if (string.IsNullOrWhiteSpace(primary.CustomerName) || string.IsNullOrWhiteSpace(duplicate.CustomerName))
            return false;

        var assignedReps = primary.AssignedSalesReps
            .Concat(duplicate.AssignedSalesReps)
            .Where(rep => !string.IsNullOrWhiteSpace(rep))
            .Select(rep => rep.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        await primaryDoc.Reference.UpdateAsync(new Dictionary<string, object>
        {
            [nameof(SalesCustomer.AssignedSalesReps)] = assignedReps,
            [nameof(SalesCustomer.ContactName)] = string.IsNullOrWhiteSpace(primary.ContactName) ? duplicate.ContactName : primary.ContactName,
            [nameof(SalesCustomer.ContactPhone)] = string.IsNullOrWhiteSpace(primary.ContactPhone) ? duplicate.ContactPhone : primary.ContactPhone
        }, cancellationToken: cancellationToken);

        var callSnapshot = await firestore.Collection("sales_calls").GetSnapshotAsync(cancellationToken);
        foreach (var callDoc in callSnapshot.Documents)
        {
            var call = SalesFirestoreMapper.MapSalesCall(callDoc);
            if (string.Equals(call.AccountName?.Trim(), duplicate.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await callDoc.Reference.UpdateAsync(nameof(SalesCall.AccountName), primary.CustomerName, cancellationToken: cancellationToken);
            }
        }

        await duplicateDoc.Reference.DeleteAsync(cancellationToken: cancellationToken);
        customerListCache.InvalidateSalesCustomers();
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
            var c = SalesFirestoreMapper.MapSalesCustomer(d);
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
            var c = SalesFirestoreMapper.MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (existingDoc is not null)
        {
            var customer = SalesFirestoreMapper.MapSalesCustomer(existingDoc);
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
            .WhereEqualTo(nameof(FirestoreCustomer.CustomerName), name)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        int custNo = 0;
        if (custQuery.Documents.Count > 0)
        {
            var cust = custQuery.Documents[0].ConvertTo<FirestoreCustomer>();
            custNo = cust.CustomerNumber;
        }

        if (custNo == 0)
        {
            custNo = allSnapshot.Documents.Count > 0
                ? allSnapshot.Documents.Select(d => SalesFirestoreMapper.MapSalesCustomer(d).CustomerNumber).DefaultIfEmpty(0).Max() + 1
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
        customerListCache.InvalidateSalesCustomers();
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
            var c = SalesFirestoreMapper.MapSalesCustomer(d);
            return string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase);
        });

        if (doc is null)
            return false;

        var customer = SalesFirestoreMapper.MapSalesCustomer(doc);
        var updatedReps = customer.AssignedSalesReps
            .Where(r => !string.Equals(r, email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await doc.Reference.UpdateAsync(nameof(SalesCustomer.AssignedSalesReps), updatedReps, cancellationToken: cancellationToken);
        customerListCache.InvalidateSalesCustomers();
        return true;
    }

    // Sales Calls
}
