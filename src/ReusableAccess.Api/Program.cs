using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using ReusableAccess.Infrastructure.Identity;

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
        options.RegisterScopes(OpenIddictConstants.Scopes.Profile);

        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options
            .UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough();
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
//====================================================================

builder.Services.AddInfrastructure(connectionString);
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var applicationManager = scope.ServiceProvider
        .GetRequiredService<IOpenIddictApplicationManager>();

    const string clientId = "ReusableAccess.Web";

    if (await applicationManager.FindByClientIdAsync(clientId) is null)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "ReusableAccess Web"
        };

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

        descriptor.Requirements.Add(
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        await applicationManager.CreateAsync(descriptor);
    }
    if (app.Environment.IsDevelopment())
    {
        var userName = builder.Configuration["DevelopmentUser:UserName"];
        var nationalId = builder.Configuration["DevelopmentUser:NationalId"];
        var displayName = builder.Configuration["DevelopmentUser:DisplayName"];
        var password = builder.Configuration["DevelopmentUser:Password"];

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(nationalId) ||
            string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Development user settings are incomplete.");
        }

        await using var userScope = app.Services.CreateAsyncScope();

        var userManager = userScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var existingUser = await userManager.FindByNameAsync(userName);

        if (existingUser is null)
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                NationalId = nationalId,
                DisplayName = displayName
            };

            var result = await userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(error => error.Code));

                throw new InvalidOperationException(
                    $"Could not create the development user: {errors}");
            }
        }
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
