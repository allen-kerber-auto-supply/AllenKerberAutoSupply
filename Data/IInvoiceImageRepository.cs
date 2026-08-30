using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceImageRepository
{
    Task<InvoiceImageLookup?> GetInvoiceImageLookupAsync(string invoiceNumber, int storeNumber, CancellationToken cancellationToken = default);
    Task<Stream?> GetInvoiceImageStreamAsync(string invoiceNumber, int storeNumber, int pageIndex = 1, CancellationToken cancellationToken = default);
    Task<string> InsertInvoiceImageAsync(string invoiceNumber, int storeNumber, Stream imageStream, string contentType, bool invoiceOnly, int? pageIndex = null, CancellationToken cancellationToken = default);
}
