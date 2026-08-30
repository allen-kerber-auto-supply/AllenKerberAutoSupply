using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class Invoice
{
    [FirestoreProperty] public string InvoiceNumber { get; set; } = string.Empty;
    [FirestoreProperty] public int StoreNumber { get; set; }
    [FirestoreProperty] public int CustomerNumber { get; set; }
    [FirestoreProperty] public string CustomerName { get; set; } = string.Empty;
    [FirestoreProperty] public Timestamp? InvoiceDate { get; set; }
    [FirestoreProperty] public double InvoiceAmount { get; set; }
    [FirestoreProperty] public string TransactionType { get; set; } = string.Empty;
    [FirestoreProperty] public string PaymentMethod { get; set; } = string.Empty;
    [FirestoreProperty] public int EmployeeNumber { get; set; }
    [FirestoreProperty] public string PoNumber { get; set; } = string.Empty;
    [FirestoreProperty] public bool HasImages { get; set; }
    [FirestoreProperty] public string ImageObjectName { get; set; } = string.Empty;
}

[FirestoreData]
public sealed class InvoiceImagePage
{
    [FirestoreProperty] public int PageIndex { get; set; } = 1;
    [FirestoreProperty] public int SqlImageId { get; set; }
    [FirestoreProperty] public string ObjectName { get; set; } = string.Empty;
    [FirestoreProperty] public string BucketName { get; set; } = string.Empty;
    [FirestoreProperty] public string ContentType { get; set; } = "image/png";
    [FirestoreProperty] public Timestamp? UploadedAt { get; set; }
}

[FirestoreData]
public sealed class InvoiceImageLookup
{
    [FirestoreProperty] public int StoreNumber { get; set; }
    [FirestoreProperty] public string InvoiceNumber { get; set; } = string.Empty;
    [FirestoreProperty] public int TotalPages { get; set; } = 1;
    [FirestoreProperty] public string ObjectName { get; set; } = string.Empty;
    [FirestoreProperty] public string BucketName { get; set; } = string.Empty;
    [FirestoreProperty] public string ContentType { get; set; } = "image/png";
    [FirestoreProperty] public Timestamp? UploadedAt { get; set; }
    [FirestoreProperty] public List<InvoiceImagePage> Pages { get; set; } = [];
}

public sealed class StatementInvoiceItem
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public double InvoiceAmount { get; set; }
    public string VendorId { get; set; } = string.Empty;
    public string StoreNumber { get; set; } = string.Empty;
    public string StatementOrInvoice { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}
