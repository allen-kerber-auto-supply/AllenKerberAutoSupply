using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Resend;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/invoices/email")]
[Authorize(Policy = AuthorizationPolicies.ActiveAccount, Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
public sealed class InvoiceEmailController(
    IResend resend,
    IInvoiceImageRepository imageRepository,
    IOptions<ResendOptions> resendOptions) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SendInvoiceEmails([FromBody] SendInvoiceEmailsRequest request, CancellationToken cancellationToken)
    {
        if (request?.Groups is null || request.Groups.Count == 0)
            return BadRequest("At least one customer group with invoices and recipients is required.");

        var fromAddress = resendOptions.Value.FromAddress;
        if (string.IsNullOrWhiteSpace(fromAddress))
            return StatusCode(StatusCodes.Status500InternalServerError, "Resend:FromAddress is not configured.");

        var results = new List<InvoiceEmailResult>();

        foreach (var group in request.Groups)
        {
            var emails = (group.Emails ?? []).Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var invoices = group.Invoices ?? [];

            if (emails.Count == 0 || invoices.Count == 0)
                continue;

            var attachments = new List<EmailAttachment>();
            foreach (var invoice in invoices)
            {
                attachments.AddRange(await BuildAttachmentsAsync(invoice, cancellationToken).ConfigureAwait(false));
            }

            if (attachments.Count == 0)
            {
                foreach (var email in emails)
                {
                    results.Add(new InvoiceEmailResult
                    {
                        CustomerNumber = group.CustomerNumber,
                        Email = email,
                        Success = false,
                        Error = "No invoice images were available to attach."
                    });
                }
                continue;
            }

            var invoiceList = string.Join(", ", invoices.Select(i => i.InvoiceNumber));
            var customerName = string.IsNullOrWhiteSpace(group.CustomerName) ? "your account" : group.CustomerName;

            foreach (var email in emails)
            {
                try
                {
                    var message = new EmailMessage
                    {
                        From = fromAddress,
                        Subject = $"Invoice{(invoices.Count == 1 ? "" : "s")} from Allen & Kerber Auto Supply ({invoiceList})",
                        HtmlBody = $"<p>Please find attached invoice{(invoices.Count == 1 ? "" : "s")} {System.Net.WebUtility.HtmlEncode(invoiceList)} for {System.Net.WebUtility.HtmlEncode(customerName)}.</p><p>Please do not reply to this email.</p>",
                        Attachments = attachments,
                    };
                    message.To.Add(email);

                    await resend.EmailSendAsync(message, cancellationToken).ConfigureAwait(false);
                    results.Add(new InvoiceEmailResult { CustomerNumber = group.CustomerNumber, Email = email, Success = true });
                }
                catch (Exception ex)
                {
                    results.Add(new InvoiceEmailResult { CustomerNumber = group.CustomerNumber, Email = email, Success = false, Error = ex.Message });
                }
            }
        }

        return Ok(results);
    }

    private async Task<List<EmailAttachment>> BuildAttachmentsAsync(InvoiceAttachmentRequest invoice, CancellationToken cancellationToken)
    {
        var attachments = new List<EmailAttachment>();
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            return attachments;

        var lookup = await imageRepository.GetInvoiceImageLookupAsync(invoice.InvoiceNumber, invoice.StoreNumber, cancellationToken).ConfigureAwait(false);
        var totalPages = lookup?.Pages.Count > 0 ? lookup.Pages.Count : 1;

        for (var page = 1; page <= totalPages; page++)
        {
            var stream = await imageRepository.GetInvoiceImageStreamAsync(invoice.InvoiceNumber, invoice.StoreNumber, page, cancellationToken).ConfigureAwait(false);
            if (stream is null)
                continue;

            await using (stream)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                var bytes = buffer.ToArray();
                if (bytes.Length == 0)
                    continue;

                var contentType = lookup?.Pages.Count > 0
                    ? (lookup.Pages.FirstOrDefault(p => p.PageIndex == page)?.ContentType ?? "image/png")
                    : (lookup?.ContentType ?? "image/png");
                var extension = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "png";
                var filename = totalPages > 1
                    ? $"{invoice.InvoiceNumber}-page{page}.{extension}"
                    : $"{invoice.InvoiceNumber}.{extension}";

                attachments.Add(new EmailAttachment
                {
                    Filename = filename,
                    Content = bytes,
                    ContentType = contentType,
                });
            }
        }

        return attachments;
    }
}

public sealed class SendInvoiceEmailsRequest
{
    public List<InvoiceEmailGroup> Groups { get; set; } = [];
}

public sealed class InvoiceEmailGroup
{
    public int CustomerNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<InvoiceAttachmentRequest> Invoices { get; set; } = [];
    public List<string> Emails { get; set; } = [];
}

public sealed class InvoiceAttachmentRequest
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int StoreNumber { get; set; }
}

public sealed class InvoiceEmailResult
{
    public int CustomerNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
