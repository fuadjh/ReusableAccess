using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using ReusableAccess.Web.Components;

var builder = WebApplication.CreateBuilder(args);
var clientSecret =
    builder.Configuration["Authentication:OpenIddict:ClientSecret"];

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException(
        "OpenIddict client secret is not configured for the Web app.");
}

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
        options.SaveTokens = false;

        options.Scope.Add("profile");
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
