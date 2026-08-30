using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class SalesRep
{
    [FirestoreProperty] public int Id { get; set; }
    [FirestoreProperty] public string RepName { get; set; } = string.Empty;
    [FirestoreProperty] public string RepEmail { get; set; } = string.Empty;
    [FirestoreProperty] public string Status { get; set; } = "A";
}

[FirestoreData]
public sealed class SalesCustomer
{
    [FirestoreProperty] public int CustomerNumber { get; set; }
    [FirestoreProperty] public string CustomerName { get; set; } = string.Empty;
    [FirestoreProperty] public string Guid { get; set; } = string.Empty;
    [FirestoreProperty] public List<string> AssignedSalesReps { get; set; } = [];
}

[FirestoreData]
public sealed class SalesCall
{
    [FirestoreProperty] public int CallID { get; set; }
    [FirestoreProperty] public string AccountName { get; set; } = string.Empty;
    [FirestoreProperty] public Timestamp? CreatedDate { get; set; }
    [FirestoreProperty] public Timestamp? CallDate { get; set; }
    [FirestoreProperty] public string Comments { get; set; } = string.Empty;
    [FirestoreProperty] public Timestamp? FollowUpDate { get; set; }
    [FirestoreProperty] public string ContactName { get; set; } = string.Empty;
    [FirestoreProperty] public string ContactPhone { get; set; } = string.Empty;
    [FirestoreProperty] public int CallDuration { get; set; }
    [FirestoreProperty] public int SalesRepId { get; set; }
    [FirestoreProperty] public string SalesRepEmail { get; set; } = string.Empty;
    [FirestoreProperty] public int Status { get; set; }
    [FirestoreProperty] public bool IsProspect { get; set; }
}

public sealed class AccountCallsSummary
{
    public int CallCount { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime? LatestCall { get; set; }
    public bool IsProspect { get; set; }
}
