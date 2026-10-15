using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IMisreadBarcodeStore
{
    Task<string> SaveAsync(Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<List<MisreadBarcodeRecord>> ListAsync(CancellationToken cancellationToken = default);
    Task<MisreadBarcodeRecord?> FindAsync(string id, CancellationToken cancellationToken = default);
    Task<Stream?> GetStreamAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
