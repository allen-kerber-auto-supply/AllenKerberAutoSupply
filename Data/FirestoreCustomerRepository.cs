using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreCustomerRepository(FirestoreDb firestore) : ICustomerRepository
{
    public async Task<IReadOnlyList<CustomerSummary>> GetInvoiceCustomerListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("customers").GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(doc =>
        {
            var customer = doc.ConvertTo<FirestoreCustomer>();
            return new CustomerSummary
            {
                CustomerNumber = customer.CustomerNumber,
                CustomerName = customer.CustomerName,
                ShowPoNumber = customer.ShowPo,
                StatementOrInvoice = customer.StatementOrInvoice
            };
        })
        .OrderBy(c => c.CustomerName)
        .ToList();
    }

    public async Task<IReadOnlyList<FirestoreCustomer>> GetAdminCustomerListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("customers").GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(doc => doc.ConvertTo<FirestoreCustomer>()).OrderBy(customer => customer.CustomerName).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCustomerEmailListAsync(int customerNumber, CancellationToken cancellationToken = default)
    {
        var doc = await firestore.Collection("customers").Document(customerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return [];

        var customer = doc.ConvertTo<FirestoreCustomer>();
        return customer.Emails;
    }

    public async Task<bool> InsertCustomerAsync(FirestoreCustomer customer, CancellationToken cancellationToken = default)
    {
        var docRef = firestore.Collection("customers").Document(customer.CustomerNumber.ToString());
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (doc.Exists)
            return false;

        Normalize(customer);
        await docRef.SetAsync(customer, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> UpdateCustomerAsync(int customerNumber, FirestoreCustomer customer, CancellationToken cancellationToken = default)
    {
        var docRef = firestore.Collection("customers").Document(customerNumber.ToString());
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return false;

        customer.CustomerNumber = customerNumber;
        Normalize(customer);
        await docRef.SetAsync(customer, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<UserInfoResult?> GetUserInfoAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return null;

        string normalizedUser = userName.Trim().ToLowerInvariant();
        var userDoc = await firestore.Collection("user_mappings").Document(normalizedUser).GetSnapshotAsync(cancellationToken);
        if (!userDoc.Exists)
            return null;

        var mapping = userDoc.ConvertTo<UserMapping>();
        var custDoc = await firestore.Collection("customers").Document(mapping.CustomerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        if (!custDoc.Exists)
            return null;

        var customer = custDoc.ConvertTo<FirestoreCustomer>();
        return new UserInfoResult
        {
            CompanyNumber = customer.CustomerNumber,
            CompanyName = customer.CustomerName
        };
    }

    private static void Normalize(FirestoreCustomer customer)
    {
        customer.CustomerName = customer.CustomerName.Trim();
        customer.VendorId = customer.VendorId.Trim();
        customer.StatementOrInvoice = string.IsNullOrWhiteSpace(customer.StatementOrInvoice) ? "I" : customer.StatementOrInvoice.Trim().ToUpperInvariant();
        customer.Address1 = customer.Address1.Trim();
        customer.Address2 = customer.Address2.Trim();
        customer.City = customer.City.Trim();
        customer.State = customer.State.Trim();
        customer.Zip = customer.Zip.Trim();
        customer.Emails = customer.Emails.Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
