using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class Customer
{
    [FirestoreProperty] public int CustomerNumber { get; set; }
    [FirestoreProperty] public string CustomerName { get; set; } = string.Empty;
    [FirestoreProperty] public bool ShowPo { get; set; }
    [FirestoreProperty] public string VendorId { get; set; } = string.Empty;
    [FirestoreProperty] public string StatementOrInvoice { get; set; } = "I";
    [FirestoreProperty] public string Address1 { get; set; } = string.Empty;
    [FirestoreProperty] public string Address2 { get; set; } = string.Empty;
    [FirestoreProperty] public string City { get; set; } = string.Empty;
    [FirestoreProperty] public string State { get; set; } = string.Empty;
    [FirestoreProperty] public string Zip { get; set; } = string.Empty;
    [FirestoreProperty] public List<string> Emails { get; set; } = [];
}

[FirestoreData]
public sealed class UserMapping
{
    [FirestoreProperty] public string UserName { get; set; } = string.Empty;
    [FirestoreProperty] public int CustomerNumber { get; set; }
}

public sealed class CustomerSummary
{
    public int CustomerNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public bool ShowPoNumber { get; set; }
    public string StatementOrInvoice { get; set; } = "I";
}

public sealed class UserInfoResult
{
    public int CompanyNumber { get; set; }
    public string CompanyName { get; set; } = string.Empty;
}
