using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Policy = AuthorizationPolicies.ActiveAccount, Roles = $"{RoleNames.SalesAdmin},{RoleNames.SalesUser}")]
public sealed class SalesController(ISalesRepository repository) : ControllerBase
{
    private const int MaxCallDurationMinutes = 8 * 60;

    private string GetEffectiveRepEmail(string? requestedRepEmail)
    {
        var currentUserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        var isSalesAdmin = User.IsInRole(RoleNames.SalesAdmin);

        if (!isSalesAdmin)
            return currentUserEmail;

        return requestedRepEmail ?? string.Empty;
    }

    // Sales Reps
    [HttpGet("reps")]
    public async Task<IActionResult> GetSalesRepList(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetSalesRepListAsync(cancellationToken));
    }

    [HttpPost("reps")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> InsertSalesRep([FromBody] CreateSalesRepRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RepEmail))
            return BadRequest("Sales rep email is required.");

        var success = await repository.InsertSalesRepAsync(request.RepName, request.RepEmail, cancellationToken);
        return success ? Ok(new { message = "Sales rep added successfully." }) : Conflict("Sales rep already exists.");
    }

    [HttpDelete("reps/{repEmail}")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> DeleteSalesRep(string repEmail, CancellationToken cancellationToken)
    {
        var success = await repository.DeleteSalesRepAsync(repEmail, cancellationToken);
        return success ? Ok(new { message = "Sales rep deleted successfully." }) : NotFound("Sales rep not found.");
    }

    // Sales Customers & Account Assignments
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomerList([FromQuery] string? salesRepEmail, CancellationToken cancellationToken)
    {
        var effectiveEmail = GetEffectiveRepEmail(salesRepEmail);
        return Ok(await repository.GetSalesCustomersAsync(effectiveEmail, cancellationToken));
    }

    [HttpGet("customers/unassigned")]
    public async Task<IActionResult> GetUnassignedCustomerList(CancellationToken cancellationToken)
    {
        var customers = await repository.GetSalesCustomersAsync(null, cancellationToken);
        var unassigned = customers
            .Where(c => c.AssignedSalesReps == null || c.AssignedSalesReps.Count == 0)
            .OrderBy(c => c.CustomerName)
            .ToList();

        return Ok(unassigned);
    }

    [HttpPost("customers")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> InsertSalesCustomer([FromBody] CreateSalesCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest("Customer name is required.");

        var success = await repository.InsertSalesCustomerAsync(request.CustomerName, request.ContactName, request.ContactPhone, cancellationToken);
        return success ? Ok(new { message = "Sales customer added successfully." }) : Conflict("Customer already exists.");
    }

    [HttpPut("customers/{customerNumber:int}")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> UpdateSalesCustomer(int customerNumber, [FromBody] UpdateSalesCustomerRequest request, CancellationToken cancellationToken)
    {
        var success = await repository.UpdateSalesCustomerAsync(customerNumber, request.CustomerName, request.ContactName, request.ContactPhone, cancellationToken);
        return success ? Ok(new { message = "Sales customer updated successfully." }) : Conflict("Customer not found or the account name is already in use.");
    }

    [HttpPost("customers/merge")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> MergeSalesCustomers([FromBody] MergeSalesCustomersRequest request, CancellationToken cancellationToken)
    {
        var success = await repository.MergeSalesCustomersAsync(request.SurvivingCustomerNumber, request.DuplicateCustomerNumber, cancellationToken);
        return success ? Ok(new { message = "Sales customers merged successfully." }) : BadRequest("Both customers must exist and be different accounts.");
    }

    [HttpDelete("customers/{customerName}")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> DeleteSalesCustomer(string customerName, CancellationToken cancellationToken)
    {
        var success = await repository.DeleteSalesCustomerAsync(customerName, cancellationToken);
        return success ? Ok(new { message = "Sales customer deleted successfully." }) : NotFound("Customer not found.");
    }

    [HttpPost("assignments")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> AssignAccount([FromBody] AccountAssignmentRequest request, CancellationToken cancellationToken)
    {
        var success = await repository.AssignAccountAsync(request.CustomerName, request.RepEmail, cancellationToken);
        return success ? Ok(new { message = "Account assigned successfully." }) : NotFound("Account not found.");
    }

    [HttpDelete("assignments")]
    [Authorize(Roles = RoleNames.SalesAdmin)]
    public async Task<IActionResult> UnAssignAccount([FromBody] AccountAssignmentRequest request, CancellationToken cancellationToken)
    {
        var success = await repository.UnAssignAccountAsync(request.CustomerName, request.RepEmail, cancellationToken);
        return success ? Ok(new { message = "Account unassigned successfully." }) : NotFound("Account not found.");
    }

    // Sales Calls
    [HttpGet("calls/{callId:int}")]
    public async Task<IActionResult> GetCallRecord(int callId, CancellationToken cancellationToken)
    {
        var record = await repository.GetCallRecordAsync(callId, cancellationToken);
        return record is not null ? Ok(record) : NotFound("Call record not found.");
    }

    [HttpGet("calls")]
    public async Task<IActionResult> GetCallRecords([FromQuery] string? salesRepEmail, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string? accountFilter, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
    {
        var effectiveEmail = GetEffectiveRepEmail(salesRepEmail);
        var calls = await repository.GetCallRecordsAsync(effectiveEmail, fromDate, toDate, cancellationToken);
        var filter = accountFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            calls = calls
                .Where(c => (c.AccountName ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            (c.ContactName ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var safePage = Math.Max(page, 1);
        return Ok(new PagedSalesCallsResponse
        {
            TotalCount = calls.Count,
            Calls = calls.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList()
        });
    }

    [HttpGet("calls/upcoming")]
    public async Task<IActionResult> GetUpcomingCallRecords([FromQuery] string? salesRepEmail, CancellationToken cancellationToken)
    {
        var effectiveEmail = GetEffectiveRepEmail(salesRepEmail);
        return Ok(await repository.GetUpComingCallRecordsAsync(effectiveEmail, cancellationToken));
    }

    [HttpGet("calls/by-account")]
    public async Task<IActionResult> GetCallsByAccount([FromQuery] string? salesRepEmail, [FromQuery] string? accountName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            return Ok(await repository.GetCallRecordsForAccountAsync(accountName, cancellationToken));
        }

        var effectiveEmail = GetEffectiveRepEmail(salesRepEmail);
        return Ok(await repository.GetCallsByAccountAsync(effectiveEmail, cancellationToken));
    }

    [HttpGet("calls/summary-by-account")]
    public async Task<IActionResult> GetAccountSummary([FromQuery] string? salesRepEmail, CancellationToken cancellationToken)
    {
        var effectiveEmail = GetEffectiveRepEmail(salesRepEmail);
        return Ok(await repository.GetAccountSummaryAsync(effectiveEmail, cancellationToken));
    }

    [HttpPost("calls")]
    public async Task<IActionResult> InsertCallRecord([FromBody] SalesCall call, CancellationToken cancellationToken)
    {
        call.AccountName = (call.AccountName ?? string.Empty).Trim().ToUpperInvariant();
        if (call.CallDuration < 0 || call.CallDuration > MaxCallDurationMinutes)
        {
            return BadRequest(new { message = "Call duration must be between 0 and 480 minutes." });
        }

        if (string.IsNullOrWhiteSpace(call.SalesRepEmail))
        {
            call.SalesRepEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(call.Comments) && call.CallDateTime.HasValue && call.CallDateTime.Value <= DateTime.UtcNow)
        {
            call.Status = 1;
        }

        var followUpTimestamp = call.FollowUpDate;
        var success = await repository.InsertCallRecordAsync(call, cancellationToken);
        if (success && followUpTimestamp.HasValue)
        {
            var followUpCall = new SalesCall
            {
                AccountName = call.AccountName,
                CallDate = followUpTimestamp,
                CallDuration = 0,
                Comments = string.Empty,
                FollowUpDate = null,
                ContactName = call.ContactName,
                ContactPhone = call.ContactPhone,
                SalesRepId = call.SalesRepId,
                SalesRepEmail = call.SalesRepEmail,
                Status = 0,
                IsProspect = call.IsProspect
            };
            await repository.InsertCallRecordAsync(followUpCall, cancellationToken);
        }

        return success ? Ok(new { message = "Call record logged successfully." }) : Conflict("Call record already exists.");
    }

    [HttpPut("calls")]
    public async Task<IActionResult> UpdateCallRecord([FromBody] SalesCall call, CancellationToken cancellationToken)
    {
        call.AccountName = (call.AccountName ?? string.Empty).Trim().ToUpperInvariant();
        if (call.CallDuration < 0 || call.CallDuration > MaxCallDurationMinutes)
        {
            return BadRequest(new { message = "Call duration must be between 0 and 480 minutes." });
        }

        if (string.IsNullOrWhiteSpace(call.SalesRepEmail))
        {
            call.SalesRepEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(call.Comments) && call.CallDateTime.HasValue && call.CallDateTime.Value <= DateTime.UtcNow)
        {
            call.Status = 1;
        }

        var followUpTimestamp = call.FollowUpDate;
        var success = await repository.UpdateCallRecordAsync(call, cancellationToken);
        if (success && followUpTimestamp.HasValue)
        {
            var followUpCall = new SalesCall
            {
                AccountName = call.AccountName,
                CallDate = followUpTimestamp,
                CallDuration = 0,
                Comments = string.Empty,
                FollowUpDate = null,
                ContactName = call.ContactName,
                ContactPhone = call.ContactPhone,
                SalesRepId = call.SalesRepId,
                SalesRepEmail = call.SalesRepEmail,
                Status = 0,
                IsProspect = call.IsProspect
            };
            await repository.InsertCallRecordAsync(followUpCall, cancellationToken);
        }

        return success ? Ok(new { message = "Call record updated successfully." }) : NotFound("Call record not found.");
    }

    [HttpPut("calls/{callId:int}")]
    public async Task<IActionResult> UpdateCallRecordById(int callId, [FromBody] SalesCall call, CancellationToken cancellationToken)
    {
        call.CallID = callId;
        return await UpdateCallRecord(call, cancellationToken);
    }

    [HttpDelete("calls/{callId:int}")]
    public async Task<IActionResult> DeleteCallRecord(int callId, CancellationToken cancellationToken)
    {
        var success = await repository.DeleteCallRecordAsync(callId, cancellationToken);
        return success ? Ok(new { message = "Call record deleted successfully." }) : NotFound("Call record not found.");
    }
}

public sealed class CreateSalesRepRequest
{
    public string RepName { get; set; } = string.Empty;
    public string RepEmail { get; set; } = string.Empty;
}

public sealed class CreateSalesCustomerRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}

public sealed class UpdateSalesCustomerRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}

public sealed class MergeSalesCustomersRequest
{
    public int SurvivingCustomerNumber { get; set; }
    public int DuplicateCustomerNumber { get; set; }
}

public sealed class AccountAssignmentRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string RepEmail { get; set; } = string.Empty;
}
