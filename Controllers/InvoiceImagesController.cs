using AllenKerberAutoSupply.Options;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/invoice-images")]
[Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
public sealed class InvoiceImagesController(
    StorageClient storage,
    IOptions<GoogleCloudOptions> options) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile image,
        [FromForm] string invoiceNumber,
        CancellationToken cancellationToken)
    {
        if (image.Length == 0 || string.IsNullOrWhiteSpace(invoiceNumber))
            return BadRequest("An image and invoice number are required.");

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Only JPG, PNG, and PDF files are supported.");

        var objectName = $"invoices/{invoiceNumber.Trim()}/{Guid.NewGuid():N}{extension}";
        await using var stream = image.OpenReadStream();
        await storage.UploadObjectAsync(
            options.Value.ImageBucket,
            objectName,
            image.ContentType,
            stream,
            cancellationToken: cancellationToken);
        return Ok(new { objectName });
    }
}
