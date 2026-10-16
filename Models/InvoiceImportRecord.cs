namespace AllenKerberAutoSupply.Models;

public sealed record InvoiceImportRecord(
    int CustomerNumber,
    string InvoiceNumber,
    DateTime InvoiceDate,
    decimal InvoiceAmount,
    string TransactionType,
    int EmployeeId,
    string PaymentMethod,
    string PoNumber);
