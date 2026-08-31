using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Options;
using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Resend;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<GoogleCloudOptions>(builder.Configuration.GetSection(GoogleCloudOptions.SectionName));
builder.Services.Configure<ExternalAuthOptions>(builder.Configuration.GetSection(ExternalAuthOptions.SectionName));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
var googleCloud = builder.Configuration.GetSection(GoogleCloudOptions.SectionName).Get<GoogleCloudOptions>()
    ?? throw new InvalidOperationException("GoogleCloud configuration is required.");
await LoadConfiguredSecretsAsync(builder.Configuration, googleCloud.ProjectId);
await LoadResendSecretAsync(builder.Configuration, googleCloud.ProjectId);
builder.Services.AddResend(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiKey"] ?? string.Empty;
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleCloudOptions>>().Value;
    return new FirestoreDbBuilder { ProjectId = options.ProjectId, DatabaseId = options.FirestoreDatabase }.Build();
});
builder.Services.AddSingleton(StorageClient.Create());
builder.Services.AddSingleton<IInvoiceRepository, FirestoreInvoiceRepository>();
builder.Services.AddSingleton<IInvoiceImageRepository, FirestoreInvoiceImageRepository>();
builder.Services.AddSingleton<ICustomerRepository, FirestoreCustomerRepository>();
builder.Services.AddSingleton<ISalesRepository, FirestoreSalesRepository>();
builder.Services.AddSingleton<IUserRoleStore, FirestoreUserRoleStore>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<UserAccount>,
    Microsoft.AspNetCore.Identity.PasswordHasher<UserAccount>>();
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.Name = "__Host-AllenKerberAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
IfConfigured(authBuilder, "Google", builder.Configuration, ConfigureGoogle);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ActiveAccount, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context =>
            !context.User.HasClaim(AuthenticationClaims.PasswordChangeRequired, bool.TrueString)));
    options.AddPolicy(AuthorizationPolicies.UserManagement, policy =>
        policy.RequireRole(RoleNames.Administrators).RequireAssertion(context =>
            !context.User.HasClaim(AuthenticationClaims.PasswordChangeRequired, bool.TrueString)));
});
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddSpaStaticFiles(options => options.RootPath = "ClientApp/dist");

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(exception, "Unhandled exception processing {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem("An unexpected error occurred. Please try again.").ExecuteAsync(context);
}));
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSpaStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/auth/login", () => Results.Redirect("/auth/external/Google")).AllowAnonymous();
    endpoints.MapGet("/api/auth/me", (HttpContext context) => Results.Ok(new
    {
        authenticated = context.User.Identity?.IsAuthenticated ?? false,
        name = context.User.Identity?.Name,
        email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
        roles = context.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value),
        mustChangePassword = context.User.HasClaim(AuthenticationClaims.PasswordChangeRequired, bool.TrueString)
    })).AllowAnonymous();
    endpoints.MapControllers();
    endpoints.MapHealthChecks("/healthz");
});
app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";
    if (app.Environment.IsDevelopment())
        spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
});
using (var scope = app.Services.CreateScope())
{
    var roleStore = scope.ServiceProvider.GetRequiredService<IUserRoleStore>();
}
app.Run();

static void IfConfigured(AuthenticationBuilder authBuilder, string providerName, IConfiguration configuration, Action<OAuthOptions, IConfiguration> configure)
{
    if (!IsProviderConfigured(configuration, providerName))
        return;

    authBuilder.AddOAuth(providerName, options => configure(options, configuration));
}

static bool IsProviderConfigured(IConfiguration configuration, string providerName)
{
    var provider = configuration.GetSection($"ExternalAuth:{providerName}");
    var clientId = provider["ClientId"] ?? string.Empty;
    var clientSecret = provider["ClientSecret"] ?? string.Empty;
    return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
}

static async Task LoadConfiguredSecretsAsync(IConfiguration configuration, string projectId)
{
    var google = configuration.GetSection("ExternalAuth:Google");
    if (!string.IsNullOrWhiteSpace(google["ClientSecret"]) || string.IsNullOrWhiteSpace(google["SecretName"]))
        return;

    if (string.IsNullOrWhiteSpace(projectId))
        throw new InvalidOperationException("GoogleCloud:ProjectId is required to load authentication secrets.");

    var secretName = google["SecretName"]!.Trim();
    var secretVersion = new Google.Cloud.SecretManager.V1.SecretVersionName(projectId, secretName, "latest");
    var client = await Google.Cloud.SecretManager.V1.SecretManagerServiceClient.CreateAsync();
    var version = await client.AccessSecretVersionAsync(secretVersion);
    var secret = version.Payload.Data.ToStringUtf8();
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException($"Secret Manager secret '{secretName}' is empty.");

    configuration["ExternalAuth:Google:ClientSecret"] = secret;
}

static async Task LoadResendSecretAsync(IConfiguration configuration, string projectId)
{
    var resend = configuration.GetSection("Resend");
    if (!string.IsNullOrWhiteSpace(resend["ApiKey"]) || string.IsNullOrWhiteSpace(resend["SecretName"]))
        return;

    if (string.IsNullOrWhiteSpace(projectId))
        throw new InvalidOperationException("GoogleCloud:ProjectId is required to load the Resend API key.");

    var secretName = resend["SecretName"]!.Trim();
    var secretVersion = new Google.Cloud.SecretManager.V1.SecretVersionName(projectId, secretName, "latest");
    var client = await Google.Cloud.SecretManager.V1.SecretManagerServiceClient.CreateAsync();
    var version = await client.AccessSecretVersionAsync(secretVersion);
    var secret = version.Payload.Data.ToStringUtf8();
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException($"Secret Manager secret '{secretName}' is empty.");

    configuration["Resend:ApiKey"] = secret;
}

static void ConfigureGoogle(OAuthOptions options, IConfiguration configuration)
{
    var provider = configuration.GetSection("ExternalAuth:Google");
    options.ClientId = provider["ClientId"] ?? string.Empty;
    options.ClientSecret = provider["ClientSecret"] ?? string.Empty;
    options.CallbackPath = "/auth/google-callback";
    options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    options.TokenEndpoint = "https://oauth2.googleapis.com/token";
    options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
    options.Scope.Add("openid"); options.Scope.Add("email"); options.Scope.Add("profile");
    ConfigureExternalTicket(options);
}

static void ConfigureExternalTicket(OAuthOptions options)
{
    options.ClaimActions.MapJsonKey(System.Security.Claims.ClaimTypes.Email, "email");
    options.ClaimActions.MapJsonKey(System.Security.Claims.ClaimTypes.Name, "name");
    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            if (string.IsNullOrWhiteSpace(context.AccessToken))
                throw new InvalidOperationException("The external provider did not return an access token.");

            using var request = new HttpRequestMessage(HttpMethod.Get, options.UserInformationEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();
            using var user = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            context.RunClaimActions(user.RootElement);
            var email = user.RootElement.GetProperty("email").GetString();
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("The external provider did not return an email address.");
            var account = await context.HttpContext.RequestServices.GetRequiredService<IUserRoleStore>()
                .FindAsync(email, context.HttpContext.RequestAborted);
            if (account is null || !account.Roles.Any(RoleNames.All.Contains))
                throw new InvalidOperationException("The authenticated email is not authorized.");
            var identity = (System.Security.Claims.ClaimsIdentity)context.Principal!.Identity!;
            foreach (var role in account.Roles.Where(RoleNames.All.Contains))
                identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
        },
        OnRemoteFailure = context =>
        {
            context.Response.Redirect("/access-denied");
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
}
