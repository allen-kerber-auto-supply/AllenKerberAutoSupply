using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IMisreadBarcodeService
{
    Task<string> SaveMisreadBarcodeAsync(Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<List<MisreadBarcodeRecord>> ListMisreadBarcodesAsync(CancellationToken cancellationToken = default);
    Task<MisreadBarcodeRecord?> GetMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default);
    Task<Stream?> GetMisreadBarcodeStreamAsync(string id, CancellationToken cancellationToken = default);
    Task<string> ResolveMisreadBarcodeAsync(string id, string invoiceNumber, int storeNumber, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task DeleteMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default);
}
