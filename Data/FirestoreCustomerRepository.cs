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
            var customer = doc.ConvertTo<Customer>();
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

    public async Task<IReadOnlyList<string>> GetCustomerEmailListAsync(int customerNumber, CancellationToken cancellationToken = default)
    {
        var doc = await firestore.Collection("customers").Document(customerNumber.ToString()).GetSnapshotAsync(cancellationToken);
        if (!doc.Exists)
            return [];

        var customer = doc.ConvertTo<Customer>();
        return customer.Emails;
    }

    public async Task<bool> InsertCustomerAsync(int customerNumber, string customerName, CancellationToken cancellationToken = default)
    {
        var docRef = firestore.Collection("customers").Document(customerNumber.ToString());
        var doc = await docRef.GetSnapshotAsync(cancellationToken);
        if (doc.Exists)
            return false;

        var customer = new Customer
        {
            CustomerNumber = customerNumber,
            CustomerName = (customerName ?? string.Empty).Trim(),
            ShowPo = false,
            StatementOrInvoice = "I",
            Emails = []
        };

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

        var customer = custDoc.ConvertTo<Customer>();
        return new UserInfoResult
        {
            CompanyNumber = customer.CustomerNumber,
            CompanyName = customer.CustomerName
        };
    }
}
