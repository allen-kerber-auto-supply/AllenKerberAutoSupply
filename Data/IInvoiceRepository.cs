using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> FindAsync(string? invoiceNumber, string? customerNumber, CancellationToken cancellationToken);
}
