using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Api;
using ReusableAccess.Api.Authorization;
using ReusableAccess.Api.Endpoints;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Authorization;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

const string apiResource = "urn:reusableaccess:api";

var builder = WebApplication.CreateBuilder(args);

// ۱) سرویس‌های پایهٔ API و مستندات
builder.Services.AddDataProtection();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// ۲) خواندن تنظیمات اتصال و secret کلاینت
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

var clientSecret =
    builder.Configuration["Authentication:OpenIddict:ClientSecret"];

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException(
        "OpenIddict client secret is not configured.");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured.");
}

// ۳) ثبت Authorization Server و اعتبارسنجی توکن در همین API
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
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// ۴) کوکی ورود کاربر در خود Authorization Server
builder.Services
    .AddAuthentication()
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = ".ReusableAccess.Identity";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
    });

// ۵) ثبت Infrastructure، دیتابیس و Identity
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddReusableAccessAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<
    IAuthorizationHandler,
    PlatformSuperAdminAuthorizationHandler>();

// ۷) Policyهای API:
// AccessApi فقط scope پروتکلی را بررسی می‌کند.
// Permission:diagnostics.read علاوه بر scope، permission دیتابیسی را هم بررسی می‌کند.
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

    options.AddPolicy("Permission:diagnostics.read", policy =>
    {
        policy
            .AddAuthenticationSchemes(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasScope("access_api"))
            .AddRequirements(
                new PermissionRequirement("diagnostics.read"));
    });
    options.AddPolicy("Permission:workitems.read", policy =>
    {
        policy
            .AddAuthenticationSchemes(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasScope("access_api"))
            .AddRequirements(
                new PermissionRequirement("workitems.read"));
    });
    options.AddPolicy("PlatformAdmin", policy =>
    {
        policy
            .AddAuthenticationSchemes(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasScope("access_api"))
            .AddRequirements(new PlatformSuperAdminRequirement());
    });
});

var app = builder.Build();

await PlatformSuperAdminSeeder.SeedAsync(
    app.Services,
    app.Configuration);

// ۸) ثبت/به‌روزرسانی کلاینت Web و scope مربوط به API در OpenIddict
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

// ۹) فقط در Development، تخصیص آزمایشی مجوز را برای کاربر موجود بساز.
// developmentScope و userManager همین‌جا تعریف شده‌اند و بیرون از این بلوک استفاده نمی‌شوند.
if (app.Environment.IsDevelopment())
{
    var developmentUserName =
        builder.Configuration["DevelopmentUser:UserName"];

    if (string.IsNullOrWhiteSpace(developmentUserName))
    {
        throw new InvalidOperationException(
            "DevelopmentUser:UserName is not configured.");
    }

    await using var developmentScope =
        app.Services.CreateAsyncScope();

    var userManager = developmentScope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    var seededUser =
        await userManager.FindByNameAsync(developmentUserName);

    if (seededUser is null)
    {
        throw new InvalidOperationException(
            "The configured development user was not found.");
    }

    await ReusableAccess.Api.DevelopmentAccessSeeder.SeedAsync(
        developmentScope.ServiceProvider,
        seededUser.Id);
}

// ۱۰) middlewareها و endpointها
// فهرست نقش‌ها و محدوده‌های فعالِ تخصیص‌یافته به کاربرِ همین access token.
// ورودی کاربر برای تعیین UserId پذیرفته نمی‌شود؛ شناسه از claim «sub» توکن می‌آید.
app.MapAccessProfilesEndpoint();
app.MapAdminRolePermissionsEndpoint();  
app.MapAdminRolesEndpoints();
app.MapWorkItemsEndpoint();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapAdminScopesEndpoints();
app.MapAdminBundlesEndpoints();
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