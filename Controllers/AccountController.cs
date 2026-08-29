using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
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
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Credentials input, CancellationToken cancellationToken)
    {
        var email = FirestoreUserRoleStore.Normalize(input.Email);
        if (!IsEmail(email) || input.Password.Length < 12)
            return BadRequest("Use a valid email address and a password of at least 12 characters.");
        if (await users.FindAsync(email, cancellationToken) is not null)
            return Conflict("An account already exists for this email.");
        var user = new UserAccount { Email = email, DisplayName = email };
        user.PasswordHash = passwordHasher.HashPassword(user, input.Password);
        await users.UpsertAsync(user, cancellationToken);
        return await SignIn(user);
    }

    [HttpPost("password-login")]
    public async Task<IActionResult> PasswordLogin([FromBody] Credentials input, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(FirestoreUserRoleStore.Normalize(input.Email), cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid email or password.");
        return await SignIn(user);
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

    private async Task<IActionResult> SignIn(UserAccount user)
    {
        if (user.Roles.Count == 0)
            return Unauthorized("Your account has not been assigned a role.");
        var claims = new List<Claim> { new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Email, user.Email) };
        claims.AddRange(user.Roles.Where(RoleNames.All.Contains).Select(role => new Claim(ClaimTypes.Role, role)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Ok(new { roles = user.Roles });
    }

    private bool IsProviderConfigured(string providerName)
    {
        var provider = configuration.GetSection($"ExternalAuth:{providerName}");
        var clientId = provider["ClientId"] ?? string.Empty;
        var clientSecret = provider["ClientSecret"] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    private static bool IsEmail(string value) => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
}

public sealed record Credentials(string Email, string Password);
