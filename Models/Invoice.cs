using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class Invoice
{
    [FirestoreProperty] public string InvoiceNumber { get; set; } = string.Empty;
    [FirestoreProperty] public string CustomerNumber { get; set; } = string.Empty;
    [FirestoreProperty] public string CustomerName { get; set; } = string.Empty;
    [FirestoreProperty] public Timestamp? InvoiceDate { get; set; }
    [FirestoreProperty] public double InvoiceAmount { get; set; }
    [FirestoreProperty] public string ImageObjectName { get; set; } = string.Empty;
}
