using AllenKerberAutoSupply.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Policy = AuthorizationPolicies.ActiveAccount, Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
public sealed class InvoicesController(IInvoiceRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? invoiceNumber, [FromQuery] string? customerNumber, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            if (!string.IsNullOrWhiteSpace(customerNumber) && int.TryParse(customerNumber, out int custNo))
            {
                return Ok(await repository.GetInvoiceDataByInvoiceNumberAndCustomerAsync(invoiceNumber, custNo, cancellationToken));
            }
            return Ok(await repository.GetInvoiceDataByInvoiceNumberAsync(invoiceNumber, cancellationToken));
        }

        return Ok(await repository.FindAsync(invoiceNumber, customerNumber, cancellationToken));
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetByDate([FromQuery] DateTime beginDate, [FromQuery] DateTime endDate, [FromQuery] int? customerNumber, CancellationToken cancellationToken)
    {
        if (customerNumber.HasValue)
        {
            return Ok(await repository.GetInvoiceDataByDtmAndCustomerAsync(beginDate, endDate, customerNumber.Value, cancellationToken));
        }

        return Ok(await repository.GetInvoiceDataByDtmAsync(beginDate, endDate, cancellationToken));
    }

    [HttpGet("statement")]
    public async Task<IActionResult> GetStatementInvoices([FromQuery] int customerNumber, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string? invoiceNumbers, CancellationToken cancellationToken)
    {
        return Ok(await repository.GetStatementInvoicesAsync(customerNumber, fromDate, toDate, invoiceNumbers ?? string.Empty, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.InvoiceAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerNumber <= 0 || string.IsNullOrWhiteSpace(request.InvoiceNumber) || request.StoreNumber <= 0)
            return BadRequest("Customer number, invoice number, and store number are required.");

        var success = await repository.InsertInvoiceDataAsync(
            request.CustomerNumber,
            request.InvoiceNumber,
            request.InvoiceDate,
            request.InvoiceAmount,
            request.TransactionType,
            request.EmployeeId,
            request.StoreNumber,
            request.PaymentMethod,
            request.PoNumber,
            cancellationToken);

        return success ? Ok(new { message = "Invoice inserted successfully." }) : Conflict("Invoice already exists for this store.");
    }
}

public sealed class CreateInvoiceRequest
{
    public int CustomerNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal InvoiceAmount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public int StoreNumber { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
}
