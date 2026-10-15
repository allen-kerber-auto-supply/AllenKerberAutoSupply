using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreMisreadBarcodeService(
    IMisreadBarcodeStore store,
    IInvoiceImageRepository invoiceImageRepository) : IMisreadBarcodeService
{
    public Task<string> SaveMisreadBarcodeAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default) =>
        store.SaveAsync(imageStream, fileName, contentType, cancellationToken);

    public Task<List<MisreadBarcodeRecord>> ListMisreadBarcodesAsync(CancellationToken cancellationToken = default) =>
        store.ListAsync(cancellationToken);

    public Task<MisreadBarcodeRecord?> GetMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default) =>
        store.FindAsync(id, cancellationToken);

    public Task<Stream?> GetMisreadBarcodeStreamAsync(string id, CancellationToken cancellationToken = default) =>
        store.GetStreamAsync(id, cancellationToken);

    public async Task<string> ResolveMisreadBarcodeAsync(
        string id,
        string invoiceNumber,
        int storeNumber,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A misread barcode id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("An invoice number is required.", nameof(invoiceNumber));
        if (storeNumber <= 0)
            throw new ArgumentException("A store number is required.", nameof(storeNumber));
        if (pageNumber <= 0)
            throw new ArgumentException("A page number must be greater than zero.", nameof(pageNumber));

        var record = await store.FindAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The selected misread barcode record no longer exists.");

        using var stream = await store.GetStreamAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The selected image could not be loaded from storage.");

        stream.Position = 0;
        var destination = await invoiceImageRepository.InsertInvoiceImageAsync(
            invoiceNumber.Trim(),
            storeNumber,
            stream,
            record.ContentType,
            false,
            pageNumber,
            cancellationToken);
        await store.DeleteAsync(id, cancellationToken);
        return destination;
    }

    public Task DeleteMisreadBarcodeAsync(string id, CancellationToken cancellationToken = default) =>
        store.DeleteAsync(id, cancellationToken);
}
