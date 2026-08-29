using AllenKerberAutoSupply.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
public sealed class InvoicesController(IInvoiceRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? invoiceNumber, [FromQuery] string? customerNumber, CancellationToken cancellationToken)
        => Ok(await repository.FindAsync(invoiceNumber, customerNumber, cancellationToken));
}
