namespace AllenKerberAutoSupply.Models;

public static class InvoiceRules
{
    public static bool IsChargeInvoice(string? transactionType, string? paymentMethod)
    {
        var transaction = (transactionType ?? string.Empty).Trim();
        var payment = (paymentMethod ?? string.Empty).Trim();

        return (string.Equals(transaction, "CHARGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(payment, "CHARGE", StringComparison.OrdinalIgnoreCase))
            && !string.Equals(transaction, "CASH", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payment, "CASH", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payment, "CHECK", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(transaction, "VOID", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsChargeInvoice(Invoice invoice)
    {
        return IsChargeInvoice(invoice.TransactionType, invoice.PaymentMethod);
    }
}
