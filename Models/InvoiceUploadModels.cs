using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class StoreUploadState
{
    [FirestoreProperty] public Dictionary<string, bool> InvoiceKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [FirestoreProperty] public Dictionary<string, bool> ImageKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [FirestoreProperty] public List<string> MissingInvoiceImages { get; set; } = [];
    [FirestoreProperty] public List<string> MissingInvoices { get; set; } = [];
    [FirestoreProperty] public Timestamp? UpdatedAt { get; set; }
}

[FirestoreData]
public sealed class StoreRecord
{
    [FirestoreProperty] public int StoreNumber { get; set; }
    [FirestoreProperty] public StoreUploadState UploadState { get; set; } = new();
}

[FirestoreData]
public sealed class MisreadBarcodeRecord
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string FileName { get; set; } = string.Empty;
    [FirestoreProperty] public string ObjectName { get; set; } = string.Empty;
    [FirestoreProperty] public string BucketName { get; set; } = string.Empty;
    [FirestoreProperty] public string ContentType { get; set; } = "image/png";
    [FirestoreProperty] public Timestamp? CreatedUtc { get; set; }
}

public sealed class InvoiceUploadReconciliation
{
    public List<InvoiceUploadMissingImage> MissingInvoiceImages { get; set; } = [];
    public List<string> MissingInvoiceImageKeys { get; set; } = [];
    public List<string> MissingInvoices { get; set; } = [];
}

public sealed class InvoiceUploadMissingImage
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public double InvoiceAmount { get; set; }
}
