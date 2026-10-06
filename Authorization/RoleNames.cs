namespace AllenKerberAutoSupply;

public static class RoleNames
{
    public const string InvoiceAdmin = "InvoiceAdmin";
    public const string InvoiceUser = "InvoiceUser";
    public const string CustomerInvoiceUser = "CustomerInvoiceUser";
    public const string SalesAdmin = "SalesAdmin";
    public const string SalesUser = "SalesUser";
    public const string TrendsUser = "TrendsUser";
    public static readonly string[] All = [InvoiceAdmin, InvoiceUser, CustomerInvoiceUser, SalesAdmin, SalesUser, TrendsUser];
    public static readonly string[] Administrators = [InvoiceAdmin, SalesAdmin];
}
