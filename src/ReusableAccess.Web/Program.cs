using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using ReusableAccess.Web.Components;
using ReusableAccess.Web.Infrastructure;
using System.Net.Http.Headers;
var builder = WebApplication.CreateBuilder(args);
var clientSecret =
    builder.Configuration["Authentication:OpenIddict:ClientSecret"];

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException(
        "OpenIddict client secret is not configured for the Web app.");
}
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<ITicketStore, DistributedCacheTicketStore>();

builder.Services
    .AddOptions<CookieAuthenticationOptions>(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, ticketStore) =>
    {
        options.SessionStore = ticketStore;
    });
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = ".ReusableAccess.Web.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://localhost:7042/";
        options.ClientId = "ReusableAccess.Web";
        options.ClientSecret = clientSecret;

        options.ResponseType = "code";
        options.UsePkce = true;

        // در این گام توکن‌ها را در کوکی Web ذخیره نمی‌کنیم.
        options.SaveTokens = true;

        options.Scope.Add("profile");
        options.Scope.Add("access_api");
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseAddress = builder.Configuration["Api:BaseAddress"]
    ?? throw new InvalidOperationException(
        "Api:BaseAddress is not configured.");

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();

app.UseAuthorization();
app.MapGet("/bff/diagnostics/protected", async (
    HttpContext context,
    IHttpClientFactory httpClientFactory) =>
{
    var accessToken = await context.GetTokenAsync("access_token");

    if (string.IsNullOrWhiteSpace(accessToken))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Access token is not available in the server-side ticket.");
    }

    var client = httpClientFactory.CreateClient("Api");

    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        "api/diagnostics/protected");

    request.Headers.Authorization =
        new AuthenticationHeaderValue("Bearer", accessToken);

    try
    {
        using var response = await client.SendAsync(
            request,
            context.RequestAborted);

        var body = await response.Content.ReadAsStringAsync(
            context.RequestAborted);

        var contentType = response.Content.Headers.ContentType?.ToString()
            ?? "application/json";

        return Results.Content(
            body,
            contentType,
            statusCode: (int)response.StatusCode);
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "The API could not be reached.");
    }
})
.RequireAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/account/login", () =>
{
    var properties = new AuthenticationProperties
    {
        RedirectUri = "/"
    };

    return Results.Challenge(
        properties,
        new[] { OpenIdConnectDefaults.AuthenticationScheme });
})
.AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
