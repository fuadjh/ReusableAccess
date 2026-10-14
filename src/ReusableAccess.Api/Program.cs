using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;
const string apiResource = "urn:reusableaccess:api";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDataProtection();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ===============connectionString =============================
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");
var clientSecret =
    builder.Configuration["Authentication:OpenIddict:ClientSecret"];

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException(
        "OpenIddict client secret is not configured.");
}
builder.Services
    .AddOpenIddict()
    .AddCore(options =>
    {
        options
            .UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>()
            .ReplaceDefaultEntities<Guid>();
    })
    .AddServer(options =>
    {
        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();
        options.RegisterScopes(
    OpenIddictConstants.Scopes.Profile,
    "access_api");
        options.RegisterResources(apiResource);
        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options
            .UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough();
    }).AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services
    .AddAuthentication()
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = ".ReusableAccess.Identity";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
    });
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AccessApi", policy =>
    {
        policy
            .AddAuthenticationSchemes(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasScope("access_api"));
    });
});
//====================================================================

builder.Services.AddInfrastructure(connectionString);
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var applicationManager = scope.ServiceProvider
        .GetRequiredService<IOpenIddictApplicationManager>();

    const string clientId = "ReusableAccess.Web";

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        ClientType = OpenIddictConstants.ClientTypes.Confidential,
        ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
        DisplayName = "ReusableAccess Web"
    };

    descriptor.AddResourcePermissions(apiResource);
    descriptor.RedirectUris.Add(
        new Uri("https://localhost:7136/signin-oidc"));

    descriptor.Permissions.Add(
        OpenIddictConstants.Permissions.Endpoints.Authorization);
    descriptor.Permissions.Add(
        OpenIddictConstants.Permissions.Endpoints.Token);
    descriptor.Permissions.Add(
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
    descriptor.Permissions.Add(
        OpenIddictConstants.Permissions.ResponseTypes.Code);
    descriptor.Permissions.Add(
        OpenIddictConstants.Permissions.Scopes.Profile);
    descriptor.Permissions.Add(
        OpenIddictConstants.Permissions.Prefixes.Scope + "access_api");

    descriptor.Requirements.Add(
        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

    var existingApplication =
        await applicationManager.FindByClientIdAsync(clientId);

    if (existingApplication is null)
    {
        await applicationManager.CreateAsync(descriptor);
    }
    else
    {
        await applicationManager.UpdateAsync(existingApplication, descriptor);
    }

    var scopeManager = scope.ServiceProvider
        .GetRequiredService<IOpenIddictScopeManager>();

    const string apiScopeName = "access_api";

    var existingScope =
        await scopeManager.FindByNameAsync(apiScopeName);

    var scopeDescriptor = new OpenIddictScopeDescriptor
    {
        Name = apiScopeName,
        DisplayName = "ReusableAccess API"
    };

    scopeDescriptor.Resources.Add(apiResource);

    if (existingScope is null)
    {
        await scopeManager.CreateAsync(scopeDescriptor);
    }
    else
    {
        await scopeManager.UpdateAsync(existingScope, scopeDescriptor);
    }
}
app.UseExceptionHandler();
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
