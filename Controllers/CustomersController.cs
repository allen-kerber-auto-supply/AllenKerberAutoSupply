using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
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

    [HttpGet("admin")]
    [Authorize(Roles = RoleNames.InvoiceAdmin)]
    public async Task<IActionResult> GetAdminCustomerList(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAdminCustomerListAsync(cancellationToken));
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
    public async Task<IActionResult> CreateCustomer([FromBody] CustomerRequest request, CancellationToken cancellationToken)
    {
        if (!IsValid(request))
            return BadRequest("Customer number and customer name are required.");

        var success = await repository.InsertCustomerAsync(request.ToCustomer(), cancellationToken);
        return success ? Ok(new { message = "Customer inserted successfully." }) : Conflict("Customer already exists.");
    }

    [HttpPut("{customerNumber:int}")]
    [Authorize(Roles = RoleNames.InvoiceAdmin)]
    public async Task<IActionResult> UpdateCustomer(int customerNumber, [FromBody] CustomerRequest request, CancellationToken cancellationToken)
    {
        if (!IsValid(request))
            return BadRequest("Customer number and customer name are required.");

        var customer = request.ToCustomer();
        var success = await repository.UpdateCustomerAsync(customerNumber, customer, cancellationToken);
        return success ? Ok(customer) : NotFound("Customer not found.");
    }

    private static bool IsValid(CustomerRequest request) => request.CustomerNumber > 0 && !string.IsNullOrWhiteSpace(request.CustomerName);
}

public sealed class CustomerRequest
{
    public int CustomerNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public bool ShowPo { get; set; }
    public string VendorId { get; set; } = string.Empty;
    public string StatementOrInvoice { get; set; } = "I";
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public List<string> Emails { get; set; } = [];

    public FirestoreCustomer ToCustomer() => new()
    {
        CustomerNumber = CustomerNumber, CustomerName = CustomerName, ShowPo = ShowPo, VendorId = VendorId,
        StatementOrInvoice = StatementOrInvoice, Address1 = Address1, Address2 = Address2, City = City,
        State = State, Zip = Zip, Emails = Emails ?? []
    };
}
