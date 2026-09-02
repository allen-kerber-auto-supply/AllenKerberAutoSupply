using Google.Cloud.Firestore;
using System.Text.Json.Serialization;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class SalesRep
{
    [FirestoreProperty] public int Id { get; set; }
    [FirestoreProperty] public string RepName { get; set; } = string.Empty;
    [FirestoreProperty] public string RepEmail { get; set; } = string.Empty;
    [FirestoreProperty] public string Status { get; set; } = "A";

    [JsonPropertyName("name")]
    public string Name
    {
        get => RepName;
        set => RepName = value ?? RepName;
    }

    [JsonPropertyName("email")]
    public string Email
    {
        get => RepEmail;
        set => RepEmail = value ?? RepEmail;
    }
}

[FirestoreData]
public sealed class SalesCustomer
{
    [FirestoreProperty] public int CustomerNumber { get; set; }
    [FirestoreProperty] public string CustomerName { get; set; } = string.Empty;
    [FirestoreProperty] public string Guid { get; set; } = string.Empty;
    [FirestoreProperty] public List<string> AssignedSalesReps { get; set; } = [];

    [JsonPropertyName("accountName")]
    public string AccountName
    {
        get => CustomerName;
        set => CustomerName = value ?? CustomerName;
    }
}

[FirestoreData]
public sealed class SalesCall
{
    [FirestoreProperty] public int CallID { get; set; }
    [FirestoreProperty] public string AccountName { get; set; } = string.Empty;
    [FirestoreProperty, JsonIgnore] public Timestamp? CreatedDate { get; set; }
    [FirestoreProperty, JsonIgnore] public Timestamp? CallDate { get; set; }
    [FirestoreProperty] public string Comments { get; set; } = string.Empty;
    [FirestoreProperty, JsonIgnore] public Timestamp? FollowUpDate { get; set; }
    [FirestoreProperty] public string ContactName { get; set; } = string.Empty;
    [FirestoreProperty] public string ContactPhone { get; set; } = string.Empty;
    [FirestoreProperty] public int CallDuration { get; set; }
    [FirestoreProperty] public int SalesRepId { get; set; }
    [FirestoreProperty] public string SalesRepEmail { get; set; } = string.Empty;
    [FirestoreProperty] public int Status { get; set; }
    [FirestoreProperty] public bool IsProspect { get; set; }

    [JsonPropertyName("id")]
    public string Id
    {
        get => CallID.ToString();
        set => CallID = int.TryParse(value, out var parsed) ? parsed : CallID;
    }

    [JsonPropertyName("phone")]
    public string Phone
    {
        get => ContactPhone;
        set => ContactPhone = value ?? string.Empty;
    }

    [JsonPropertyName("repEmail")]
    public string RepEmail
    {
        get => SalesRepEmail;
        set => SalesRepEmail = value ?? string.Empty;
    }

    [JsonPropertyName("repName")]
    public string RepName { get; set; } = string.Empty;

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDateTime
    {
        get => CreatedDate?.ToDateTime();
        set => CreatedDate = value.HasValue ? Timestamp.FromDateTime(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }

    [JsonPropertyName("callDate")]
    public DateTime? CallDateTime
    {
        get => CallDate?.ToDateTime();
        set => CallDate = value.HasValue ? Timestamp.FromDateTime(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }

    [JsonPropertyName("followUpDate")]
    public DateTime? FollowUpDateTime
    {
        get => FollowUpDate?.ToDateTime();
        set => FollowUpDate = value.HasValue ? Timestamp.FromDateTime(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }
}

public sealed class AccountCallsSummary
{
    public int CallCount { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime? LatestCall { get; set; }
    public bool IsProspect { get; set; }
}

public sealed class AccountSummaryResponse
{
    public string AccountName { get; set; } = string.Empty;
    public int TotalCalls { get; set; }
    public int CompletedCalls { get; set; }
    public int ScheduledCalls { get; set; }
    public DateTime? LastCallDate { get; set; }
    public List<SalesCall> Calls { get; set; } = [];
}
