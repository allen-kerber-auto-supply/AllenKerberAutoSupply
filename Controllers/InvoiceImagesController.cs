using AllenKerberAutoSupply.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/invoice-images")]
[Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
public sealed class InvoiceImagesController(IInvoiceImageRepository repository) : ControllerBase
{
    [HttpGet("{storeNumber:int}/{invoiceNumber}")]
    public async Task<IActionResult> GetImage(int storeNumber, string invoiceNumber, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var lookup = await repository.GetInvoiceImageLookupAsync(invoiceNumber, storeNumber, cancellationToken);
        var rawStream = await repository.GetInvoiceImageStreamAsync(invoiceNumber, storeNumber, page, cancellationToken);
        if (rawStream is null)
            return NotFound("Invoice image not found.");

        string declaredContentType = "image/png";
        if (lookup?.Pages.Count > 0)
        {
            var p = lookup.Pages.FirstOrDefault(x => x.PageIndex == page) ?? lookup.Pages[0];
            declaredContentType = p.ContentType;
        }
        else if (lookup != null && !string.IsNullOrWhiteSpace(lookup.ContentType))
        {
            declaredContentType = lookup.ContentType;
        }

        var (stream, contentType) = await NormalizeImageAsync(rawStream, declaredContentType, cancellationToken);
        return File(stream, contentType);
    }

    private static async Task<(Stream Stream, string ContentType)> NormalizeImageAsync(Stream sourceStream, string declaredContentType, CancellationToken cancellationToken)
    {
        if (string.Equals(declaredContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceStream.CanSeek) sourceStream.Position = 0;
            return (sourceStream, "application/pdf");
        }

        try
        {
            MemoryStream ms;
            if (sourceStream is MemoryStream mem && sourceStream.CanSeek)
            {
                ms = mem;
                ms.Position = 0;
            }
            else
            {
                ms = new MemoryStream();
                await sourceStream.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
            }

            var format = await Image.DetectFormatAsync(ms, cancellationToken);
            ms.Position = 0;

            if (format != null)
            {
                if (format.DefaultMimeType == "image/png" || format.DefaultMimeType == "image/jpeg" || format.DefaultMimeType == "image/webp")
                {
                    return (ms, format.DefaultMimeType);
                }

                // If it's TIFF, BMP, or any other format, convert to standard PNG for browser display
                using var image = await Image.LoadAsync(ms, cancellationToken);
                var pngStream = new MemoryStream();
                await image.SaveAsPngAsync(pngStream, cancellationToken);
                pngStream.Position = 0;
                return (pngStream, "image/png");
            }

            return (ms, string.IsNullOrWhiteSpace(declaredContentType) ? "image/png" : declaredContentType);
        }
        catch
        {
            if (sourceStream.CanSeek) sourceStream.Position = 0;
            return (sourceStream, string.IsNullOrWhiteSpace(declaredContentType) ? "image/png" : declaredContentType);
        }
    }

    [HttpGet("{invoiceNumber}")]
    public async Task<IActionResult> GetImageByInvoice(string invoiceNumber, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        return await GetImage(0, invoiceNumber, page, cancellationToken);
    }

    [HttpGet("{storeNumber:int}/{invoiceNumber}/page/{pageNumber:int}")]
    public async Task<IActionResult> GetImageByPage(int storeNumber, string invoiceNumber, int pageNumber, CancellationToken cancellationToken)
    {
        return await GetImage(storeNumber, invoiceNumber, pageNumber, cancellationToken);
    }

    [HttpGet("{invoiceNumber}/page/{pageNumber:int}")]
    public async Task<IActionResult> GetImageByInvoicePage(string invoiceNumber, int pageNumber, CancellationToken cancellationToken)
    {
        return await GetImage(0, invoiceNumber, pageNumber, cancellationToken);
    }

    [HttpGet("{storeNumber:int}/{invoiceNumber}/lookup")]
    public async Task<IActionResult> GetLookup(int storeNumber, string invoiceNumber, CancellationToken cancellationToken)
    {
        var lookup = await repository.GetInvoiceImageLookupAsync(invoiceNumber, storeNumber, cancellationToken);
        if (lookup is null)
            return NotFound("Invoice image lookup metadata not found.");

        return Ok(lookup);
    }

    [HttpGet("{invoiceNumber}/lookup")]
    public async Task<IActionResult> GetLookupByInvoice(string invoiceNumber, CancellationToken cancellationToken)
    {
        return await GetLookup(0, invoiceNumber, cancellationToken);
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile image,
        [FromForm] string invoiceNumber,
        [FromForm] int storeNumber,
        [FromForm] bool invoiceOnly = false,
        [FromForm] int? pageIndex = null,
        CancellationToken cancellationToken = default)
    {
        if (image.Length == 0 || string.IsNullOrWhiteSpace(invoiceNumber) || storeNumber <= 0)
            return BadRequest("An image, store number, and invoice number are required.");

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Only JPG, PNG, and PDF files are supported.");

        await using var stream = image.OpenReadStream();
        try
        {
            var objectName = await repository.InsertInvoiceImageAsync(
                invoiceNumber,
                storeNumber,
                stream,
                image.ContentType,
                invoiceOnly,
                pageIndex,
                cancellationToken);

            return Ok(new { objectName });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
