using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.UserManagement)]
public sealed class AdminUsersController(IUserRoleStore users, IPasswordHasher<UserAccount> passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> List(CancellationToken cancellationToken)
        => Ok((await users.ListAsync(cancellationToken)).Select(UserSummary.From));

    [HttpPost]
    public async Task<ActionResult<UserSummary>> Create([FromBody] CreateUserRequest input, CancellationToken cancellationToken)
    {
        var email = FirestoreUserRoleStore.Normalize(input.Email);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(input.DisplayName) || !IsValidPassword(input.TemporaryPassword) || !AreValidRoles(input.Roles))
            return BadRequest(new { message = "Provide a name, valid email, at least one role, and a temporary password." });
        if (await users.FindAsync(email, cancellationToken) is not null)
            return Conflict(new { message = "A user with this email already exists." });

        var user = new UserAccount { Email = email, DisplayName = input.DisplayName.Trim(), Roles = input.Roles.Distinct().ToArray(), MustChangePassword = true };
        user.PasswordHash = passwordHasher.HashPassword(user, input.TemporaryPassword);
        await users.UpsertAsync(user, cancellationToken);
        return CreatedAtAction(nameof(List), UserSummary.From(user));
    }

    [HttpPut("{email}/roles")]
    public async Task<ActionResult<UserSummary>> UpdateRoles(string email, [FromBody] UpdateRolesRequest input, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(email, cancellationToken);
        if (user is null)
            return NotFound();
        if (!AreValidRoles(input.Roles))
            return BadRequest(new { message = "Select at least one valid role." });

        user.Roles = input.Roles.Distinct().ToArray();
        await users.UpsertAsync(user, cancellationToken);
        return Ok(UserSummary.From(user));
    }

    [HttpPost("{email}/reset-password")]
    public async Task<ActionResult<UserSummary>> ResetPassword(string email, [FromBody] ResetPasswordRequest input, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(email, cancellationToken);
        if (user is null)
            return NotFound();
        if (!IsValidPassword(input.TemporaryPassword))
            return BadRequest(new { message = "The temporary password is invalid." });

        user.PasswordHash = passwordHasher.HashPassword(user, input.TemporaryPassword);
        user.MustChangePassword = true;
        await users.UpsertAsync(user, cancellationToken);
        return Ok(UserSummary.From(user));
    }

    [HttpDelete("{email}")]
    public async Task<IActionResult> Delete(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = FirestoreUserRoleStore.Normalize(email);
        var user = await users.FindAsync(normalizedEmail, cancellationToken);
        if (user is null)
            return NotFound();
        if (string.Equals(normalizedEmail, FirestoreUserRoleStore.Normalize(User.FindFirstValue(ClaimTypes.Email) ?? string.Empty), StringComparison.Ordinal))
            return BadRequest(new { message = "You cannot delete your own account." });

        await users.DeleteAsync(normalizedEmail, cancellationToken);
        return NoContent();
    }

    private static bool AreValidRoles(IReadOnlyList<string> roles) => roles.Count > 0 && roles.All(RoleNames.All.Contains);

    // Matches the generated ZZZZ-ZZZZ temporary password format (4 alphanumeric, dash, 4 alphanumeric).
    private static readonly Regex TemporaryPasswordFormat = new("^[A-Z0-9]{4}-[A-Z0-9]{4}$", RegexOptions.Compiled);
    private static bool IsValidPassword(string password) => !string.IsNullOrWhiteSpace(password) && TemporaryPasswordFormat.IsMatch(password);
}

public sealed record CreateUserRequest(string Email, string DisplayName, string TemporaryPassword, IReadOnlyList<string> Roles);
public sealed record UpdateRolesRequest(IReadOnlyList<string> Roles);
public sealed record ResetPasswordRequest(string TemporaryPassword);
public sealed record UserSummary(string Email, string DisplayName, IReadOnlyList<string> Roles, bool MustChangePassword)
{
    public static UserSummary From(UserAccount user) => new(user.Email, user.DisplayName, user.Roles, user.MustChangePassword);
}
