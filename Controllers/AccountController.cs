using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("auth")]
public sealed class AccountController(
    IUserRoleStore users,
    IPasswordHasher<UserAccount> passwordHasher,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("password-login")]
    public async Task<IActionResult> PasswordLogin([FromBody] Credentials input, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(FirestoreUserRoleStore.Normalize(input.Email), cancellationToken);
        if (user is null)
            return AccessDenied();
        if (string.IsNullOrWhiteSpace(user.PasswordHash)
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid email or password.");
        return await SignIn(user, user.MustChangePassword);
    }

    [HttpGet("external/{provider}")]
    public IActionResult ExternalLogin(string provider, string? returnUrl = "/")
    {
        var normalizedProvider = provider.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProvider))
            return BadRequest("Provider is required.");

        var scheme = normalizedProvider switch
        {
            "Google" => "Google",
            _ => null
        };

        if (scheme is null || !IsProviderConfigured(scheme))
            return BadRequest($"Authentication provider '{provider}' is not configured.");

        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }, scheme);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.CurrentPassword) || !IsValidPassword(input.NewPassword))
            return BadRequest(new { message = "Enter your current password and a new password of at least 12 characters." });

        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = email is null ? null : await users.FindAsync(email, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash)
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "The current password is incorrect." });

        user.PasswordHash = passwordHasher.HashPassword(user, input.NewPassword);
        user.MustChangePassword = false;
        await users.UpsertAsync(user, cancellationToken);
        return await SignIn(user);
    }

    private async Task<IActionResult> SignIn(UserAccount user, bool passwordChangeRequired = false)
    {
        if (user.Roles.Count == 0)
            return AccessDenied();
        var claims = new List<Claim> { new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Email, user.Email) };
        claims.AddRange(user.Roles.Where(RoleNames.All.Contains).Select(role => new Claim(ClaimTypes.Role, role)));
        if (passwordChangeRequired)
            claims.Add(new Claim(AuthenticationClaims.PasswordChangeRequired, bool.TrueString));
        if (claims.All(claim => claim.Type != ClaimTypes.Role))
            return AccessDenied();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Ok(new { roles = user.Roles });
    }

    private IActionResult AccessDenied()
        => StatusCode(StatusCodes.Status403Forbidden, new
        {
            code = "access_denied",
            message = "Your account is not authorized to use this application."
        });

    private bool IsProviderConfigured(string providerName)
    {
        var provider = configuration.GetSection($"ExternalAuth:{providerName}");
        var clientId = provider["ClientId"] ?? string.Empty;
        var clientSecret = provider["ClientSecret"] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    private static bool IsValidPassword(string password) => password.Length >= 12;
}

public sealed record Credentials(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
