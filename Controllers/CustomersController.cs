using AllenKerberAutoSupply.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = AuthorizationPolicies.ActiveAccount, Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser},{RoleNames.SalesAdmin},{RoleNames.SalesUser}")]
public sealed class CustomersController(ICustomerRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCustomerList(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetInvoiceCustomerListAsync(cancellationToken));
    }

    [HttpGet("{customerNumber:int}/emails")]
    public async Task<IActionResult> GetEmails(int customerNumber, CancellationToken cancellationToken)
    {
        return Ok(await repository.GetCustomerEmailListAsync(customerNumber, cancellationToken));
    }

    [HttpGet("user-info/{userName}")]
    public async Task<IActionResult> GetUserInfo(string userName, CancellationToken cancellationToken)
    {
        var result = await repository.GetUserInfoAsync(userName, cancellationToken);
        if (result is null)
            return NotFound("User company mapping not found.");

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.InvoiceAdmin)]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerNumber <= 0 || string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest("Customer number and customer name are required.");

        var success = await repository.InsertCustomerAsync(request.CustomerNumber, request.CustomerName, cancellationToken);
        return success ? Ok(new { message = "Customer inserted successfully." }) : Conflict("Customer already exists.");
    }
}

public sealed class CreateCustomerRequest
{
    public int CustomerNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}
