using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ReusableAccess.Contracts;
using ReusableAccess.Web.Ui;
using System.Security.Cryptography.X509Certificates;

namespace ReusableAccess.Web.Completion;

public sealed record ApiPermissionRequirement(string Key)
    : IAuthorizationRequirement;

public sealed class ApiPermissionHandler(ConsoleApi api)
    : AuthorizationHandler<ApiPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            await api.AllowedAsync(requirement.Key, context.User))
        {
            context.Succeed(requirement);
        }
    }
}

public static class CompletionWebExtensions
{
    public static IServiceCollection AddCompletionWeb(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        services.Configure<AuthenticationOptions>(o =>
            o.DefaultForbidScheme =
                CookieAuthenticationDefaults.AuthenticationScheme);

        services.AddScoped<ConsoleApi>();
        services.AddScoped<BundleManagementService>();
        services.AddScoped<IAuthorizationHandler, ApiPermissionHandler>();
        services.AddScoped<ManagedUsersService>();
        services.AddAuthorization(o =>
        {
            o.AddPolicy("ConsoleAdmin", p =>
                p.RequireAuthenticatedUser()
                    .AddRequirements(new ApiPermissionRequirement("admin")));

            foreach (var key in PermissionCatalog.All.Keys)
            {
                o.AddPolicy("Console:" + key, p =>
                    p.RequireAuthenticatedUser()
                        .AddRequirements(new ApiPermissionRequirement(key)));
            }
        });

        var name = config["Deployment:ApplicationName"] ?? "ReusableAccess";

        var dp = services.AddDataProtection()
            .SetApplicationName(name + ".Web");

        var keys = config["Deployment:DataProtectionKeysPath"];

        if (!string.IsNullOrWhiteSpace(keys))
            dp.PersistKeysToFileSystem(new DirectoryInfo(keys));

        if (!environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(keys))
                throw new InvalidOperationException(
                    "Configure persistent Data Protection keys for Web.");

            var path = config["Certificates:DataProtection:Path"]
                ?? throw new InvalidOperationException(
                    "Configure Data Protection certificate for Web.");

            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                path, config["Certificates:DataProtection:Password"],
                X509KeyStorageFlags.EphemeralKeySet);

            if (!cert.HasPrivateKey)
                throw new InvalidOperationException(
                    "Data Protection certificate requires a private key.");

            dp.ProtectKeysWithCertificate(cert);
        }

       

        services.PostConfigure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme, o =>
            {
                o.Cookie.Name = "." +
                    string.Concat(name.Where(
                        c => char.IsAsciiLetterOrDigit(c) || c == '-')) +
                    ".Web.Auth";

                o.Events.OnRedirectToAccessDenied = async c =>
                {
                    c.Response.StatusCode = 403;
                    c.Response.ContentType = "text/html; charset=utf-8";

                    await c.Response.WriteAsync(
                        "<!doctype html><html lang='fa' dir='rtl'>" +
                        "<meta charset='utf-8'>" +
                        "<link rel='stylesheet' href='/css/access-glass.css'>" +
                        "<body><main class='ra-auth-wrap'>" +
                        "<section class='glass ra-card'>" +
                        "<h1>۴۰۳ — دسترسی مجاز نیست</h1>" +
                        "<p>این حساب مجوز مشاهدهٔ صفحه را ندارد.</p>" +
                        "<a href='/console'>بازگشت به پنل</a>" +
                        "</section></main></body></html>");
                };
            });

        return services;
    }

    public static void MapCompletionWebEndpoints(this WebApplication app)
    {
        app.Use(async (c, next) =>
        {
            c.Response.Headers["X-Content-Type-Options"] = "nosniff";
            c.Response.Headers["Referrer-Policy"] = "no-referrer";
            c.Response.Headers["Content-Security-Policy"] =
                "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

            if (c.Request.Path.StartsWithSegments("/console") ||
                c.Request.Path.StartsWithSegments("/bff"))
                c.Response.Headers.CacheControl = "no-store";

            await next(c);
        });

        app.MapPost("/console/logout",
            async (HttpContext c, IAntiforgery anti) =>
            {
                try
                {
                    await anti.ValidateRequestAsync(c);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest("درخواست معتبر نیست.");
                }

                return Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        OpenIdConnectDefaults.AuthenticationScheme
                    ]);
            }).RequireAuthorization();
    }
}