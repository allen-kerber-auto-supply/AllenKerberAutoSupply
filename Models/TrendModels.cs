using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class TrendInvoicePoint
{
    [FirestoreProperty] public string InvoiceDate { get; set; } = string.Empty;
    [FirestoreProperty] public string InvoiceNumber { get; set; } = string.Empty;
    [FirestoreProperty] public double Amount { get; set; }
    [FirestoreProperty] public string ComparisonPeriod { get; set; } = string.Empty;
}

[FirestoreData]
public sealed class TrendCustomerResult
{
    [FirestoreProperty] public int CustomerNumber { get; set; }
    [FirestoreProperty] public string CustomerName { get; set; } = string.Empty;
    [FirestoreProperty] public int PriorYearInvoiceCount { get; set; }
    [FirestoreProperty] public int CurrentInvoiceCount { get; set; }
    [FirestoreProperty] public double PriorYearAmount { get; set; }
    [FirestoreProperty] public double CurrentAmount { get; set; }
    [FirestoreProperty] public double AmountChange { get; set; }
    [FirestoreProperty] public double AmountChangePercent { get; set; }
    [FirestoreProperty] public double CurrentAggregate { get; set; }
    [FirestoreProperty] public double PriorYearAggregate { get; set; }
    [FirestoreProperty] public double AggregateChange { get; set; }
    [FirestoreProperty] public double AggregateChangePercent { get; set; }
    [FirestoreProperty] public bool IsDeclining { get; set; }
    [FirestoreProperty] public List<TrendInvoicePoint> Invoices { get; set; } = [];
}

[FirestoreData]
public sealed class TrendData
{
    [FirestoreProperty] public string RangeKey { get; set; } = string.Empty;
    [FirestoreProperty] public string FromDate { get; set; } = string.Empty;
    [FirestoreProperty] public string ToDate { get; set; } = string.Empty;
    [FirestoreProperty] public string PriorYearFromDate { get; set; } = string.Empty;
    [FirestoreProperty] public string PriorYearToDate { get; set; } = string.Empty;
    [FirestoreProperty] public Timestamp? GeneratedAt { get; set; }
    [FirestoreProperty] public string AnalysisVersion { get; set; } = "6";
    [FirestoreProperty] public string AggregateBy { get; set; } = "amount";
    [FirestoreProperty] public int CurrentInvoiceCount { get; set; }
    [FirestoreProperty] public int PriorYearInvoiceCount { get; set; }
    [FirestoreProperty] public int CustomerCount { get; set; }
    [FirestoreProperty] public int DecliningCustomerCount { get; set; }
    [FirestoreProperty] public double CurrentTotalAmount { get; set; }
    [FirestoreProperty] public double PriorYearTotalAmount { get; set; }
    [FirestoreProperty] public double CurrentTotalAggregate { get; set; }
    [FirestoreProperty] public double PriorYearTotalAggregate { get; set; }
    [FirestoreProperty] public List<TrendCustomerResult> Customers { get; set; } = [];
}

public sealed class TrendAnalysisResponse
{
    public TrendData Data { get; init; } = new();
    public bool Cached { get; init; }
}

public sealed class TrendAnalysisRequest
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public string AggregateBy { get; init; } = "amount";
}
