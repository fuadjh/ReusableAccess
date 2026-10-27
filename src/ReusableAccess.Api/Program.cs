using ReusableAccess.Api;
using ReusableAccess.Api.Authorization;
using ReusableAccess.Api.Endpoints;
using ReusableAccess.Application;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Authorization;

const string apiResource = "urn:reusableaccess:api";

var builder = WebApplication.CreateBuilder(args);

// سرویس‌های پایهٔ API
builder.Services.AddDataProtection();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddApplication();

// خواندن و اعتبارسنجی تنظیمات
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

// ثبت Infrastructure و سرویس‌های امنیتی
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddReusableAccessAuthorization();
builder.Services.AddReusableAccessOpenIddict(apiResource);
builder.Services.AddReusableAccessApiAuthorization();

var app = builder.Build();

// همهٔ initializationهای زمان startup فقط از این نقطه اجرا می‌شوند.
await app.InitializeReusableAccessAsync( clientSecret, apiResource);
app.UseReusableAccessApi();
app.Run();
