using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceImageRepository
{
    Task<InvoiceImageLookup?> GetInvoiceImageLookupAsync(string invoiceNumber, int storeNumber, CancellationToken cancellationToken = default);
    Task<Stream?> GetInvoiceImageStreamAsync(string invoiceNumber, int storeNumber, int pageIndex = 1, CancellationToken cancellationToken = default);
    Task<string> InsertInvoiceImageAsync(string invoiceNumber, int storeNumber, Stream imageStream, string contentType, bool invoiceOnly, int? pageIndex = null, CancellationToken cancellationToken = default);
    Task<string> SaveMisreadBarcodeAsync(Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<List<MisreadBarcodeRecord>> ListMisreadBarcodesAsync(CancellationToken cancellationToken = default);
    Task<MisreadBarcodeRecord?> GetMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default);
    Task<Stream?> GetMisreadBarcodeStreamAsync(string id, CancellationToken cancellationToken = default);
    Task<string> ResolveMisreadBarcodeAsync(string id, string invoiceNumber, int storeNumber, CancellationToken cancellationToken = default);
    Task DeleteMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default);
}
