namespace AllenKerberAutoSupply.Data;

internal static class InvoiceNumberNormalizer
{
    public static string NormalizeForStorage(string? invoiceNumber)
    {
        var value = (invoiceNumber ?? string.Empty).Trim();
        return value.Length > 0 && value.All(char.IsDigit)
            ? value.PadLeft(6, '0')
            : value;
    }
}
