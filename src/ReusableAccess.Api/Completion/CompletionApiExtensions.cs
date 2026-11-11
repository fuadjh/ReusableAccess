using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Infrastructure.Authorization;

namespace ReusableAccess.Api.Completion;

public sealed class CompletionExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception error, CancellationToken ct)
    {
        if (error is not CompletionException known)
            return false;

        await Results.Problem(
            statusCode: known.StatusCode,
            title: "درخواست انجام نشد",
            detail: known.Message).ExecuteAsync(context);

        return true;
    }
}

public static class CompletionApiExtensions
{
    public static IServiceCollection AddCompletionApi(
        this IServiceCollection services)
    {
        services.AddRazorComponents();
        services.AddAntiforgery();
        services.AddExceptionHandler<CompletionExceptionHandler>();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;

            o.AddPolicy("account", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 12,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0
                    }));
        });

        services.AddAuthorization(o =>
        {
            foreach (var key in PermissionCatalog.All.Keys
                         .Where(k => k is not
                             ("diagnostics.read" or "workitems.read")))
            {
                o.AddPolicy("Permission:" + key, policy =>
                    policy.AddAuthenticationSchemes(
                            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireAssertion(c => c.User.HasScope("access_api"))
                        .AddRequirements(new PermissionRequirement(key)));
            }
        });

        services.PostConfigure<CookieAuthenticationOptions>(
            IdentityConstants.ApplicationScheme, o =>
            {
                o.LoginPath = "/Account/Login";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;

                // اعتبارسنجی قبلی احتمالی را حذف نمی‌کنیم.
                var previous = o.Events.OnValidatePrincipal;

                o.Events.OnValidatePrincipal = async c =>
                {
                    var priorVersion = c.Principal?.FindFirstValue("ra_version");

                    await previous(c);

                    if (c.Principal?.Identity?.IsAuthenticated != true)
                        return;

                    var id = c.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                    var raw = c.Principal.FindFirstValue("ra_version")
                              ?? priorVersion;

                    var version = int.TryParse(
                        raw, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var parsed)
                        ? parsed : 0;

                    var validator = c.HttpContext.RequestServices
                        .GetRequiredService<IAccountSessionValidator>();

                    var valid = Guid.TryParse(id, out var userId) &&
                        await validator.IsCurrentAsync(
                            userId, version, c.HttpContext.RequestAborted);

                    if (!valid)
                    {
                        c.RejectPrincipal();
                        await c.HttpContext.SignOutAsync(
                            IdentityConstants.ApplicationScheme);
                    }
                    else if (c.Principal.FindFirst("ra_version") is null &&
                             c.Principal.Identity is ClaimsIdentity identity)
                    {
                        identity.AddClaim(new Claim(
                            "ra_version",
                            version.ToString(CultureInfo.InvariantCulture)));

                        c.ShouldRenew = true;
                    }
                };
            });

        return services;
    }

    public static WebApplication UseCompletionAccountGate(
        this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] =
                "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

            if (context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/Account"))
                context.Response.Headers.CacheControl = "no-store";

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var auth = await context.AuthenticateAsync(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

                if (auth.Succeeded)
                {
                    var version = int.TryParse(
                        auth.Principal?.FindFirst("ra_version")?.Value,
                        out var parsed) ? parsed : 0;

                    var validator = context.RequestServices
                        .GetRequiredService<IAccountSessionValidator>();

                    if (!Guid.TryParse(
                            auth.Principal?.FindFirst("sub")?.Value, out var id) ||
                        !await validator.IsCurrentAsync(
                            id, version, context.RequestAborted))
                    {
                        await Results.Problem(
                            statusCode: 401,
                            title: "نشست معتبر نیست؛ دوباره وارد شوید.")
                            .ExecuteAsync(context);
                        return;
                    }
                }
            }

            await next(context);
        });

        return app;
    }

    public static Guid Subject(HttpContext context)
        => Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new CompletionException(401, "ورود لازم است.");

    private static string Mode(HttpContext c)
        => c.Request.Headers["X-Active-Scope-Mode"].FirstOrDefault()
           ?? "selected";

    private static string? Scope(HttpContext c)
        => c.Request.Headers["X-Active-Scope"].FirstOrDefault();

    public sealed record SimpleAssignment(Guid RoleId, Guid ScopeId);

    public static WebApplication MapCompletionEndpoints(
        this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization("PlatformAdmin");

       //======

        admin.MapGet("/access-settings",
            async (ISender s, CancellationToken ct) =>
                Results.Ok(new { data = await s.Send(new GetAuthSettings(), ct) }));

        admin.MapPut("/access-settings",
            async (AuthSettingsInput input, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(new SaveAuthSettings(Subject(c), input), ct)
                }));

        admin.MapGet("/audit",
            async (int? page, ISender s, CancellationToken ct) =>
                Results.Ok(new { data = await s.Send(new ReadAudit(page ?? 1), ct) }));

        admin.MapDelete("/roles/{id:guid}",
            async (Guid id, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(new DeleteAccessEntity(Subject(c), "role", id), ct)
                }));

       

        admin.MapGet("/users/{id:guid}/access-list",
            async (Guid id, ISender s, CancellationToken ct) =>
                Results.Ok(new { data = await s.Send(new ReadBundles(id), ct) }));

        admin.MapPost("/users/{id:guid}/assign",
            async (Guid id, SimpleAssignment input, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(new SaveSimpleAssignment(
                        Subject(c), id, input.RoleId, input.ScopeId), ct)
                }));

        app.MapGet("/api/console/check/admin", () => Results.NoContent())
            .RequireAuthorization("PlatformAdmin");

        foreach (var key in PermissionCatalog.All.Keys)
        {
            app.MapGet("/api/console/check/" + key, () => Results.NoContent())
                .RequireAuthorization("Permission:" + key);
        }

        var account = app.MapGroup("/api/account")
            .RequireAuthorization("AccessApi");

        account.MapGet("/me",
            async (HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(new GetCurrentUser(Subject(c)), ct)
                }));

        account.MapPost("/password",
            async (ChangePasswordInput input, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(new ChangeOwnPassword(Subject(c), input), ct)
                }));

        account.MapPost("/contact/request",
            async (ContactRequest input, HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = new ChallengeIssued(
                        await w.RequestContactAsync(Subject(c), input.Channel, ct))
                }));

        account.MapPost("/contact/confirm",
            async (ChallengeAnswer input, HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                await w.ConfirmContactAsync(
                    Subject(c), input.ChallengeId, input.Code, ct);
                return Results.Ok(new { data = true });
            });

        app.MapGet("/api/console/work-items",
            async (HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(
                        new ReadConsoleWorkItems(Mode(c), Scope(c)), ct)
                }))
            .RequireAuthorization("Permission:workitems.read");

        app.MapGet("/api/console/writable-scopes",
            async (HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(
                        new ReadWritableScopes(Mode(c), Scope(c)), ct)
                }))
            .RequireAuthorization("Permission:workitems.create");

        app.MapPost("/api/work-items",
            async (WorkItemInput input, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(
                        new WriteWorkItem(null, Mode(c), Scope(c), input), ct)
                }))
            .RequireAuthorization("Permission:workitems.create");

        app.MapPut("/api/work-items/{id:guid}",
            async (Guid id, WorkItemInput input, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(
                        new WriteWorkItem(id, Mode(c), Scope(c), input), ct)
                }))
            .RequireAuthorization("Permission:workitems.update");

        app.MapDelete("/api/work-items/{id:guid}",
            async (Guid id, HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(
                        new RemoveWorkItem(id, Mode(c), Scope(c)), ct)
                }))
            .RequireAuthorization("Permission:workitems.delete");

        app.MapGet("/api/reports/summary",
            async (HttpContext c, ISender s, CancellationToken ct) =>
                Results.Ok(new
                {
                    data = await s.Send(
                        new ReadReport(Mode(c), Scope(c)), ct)
                }))
            .RequireAuthorization("Permission:reports.read");

        return app;
    }

    public static WebApplication MapCompletionPresentation(
        this WebApplication app)
    {
        app.UseRateLimiter();
        app.UseAntiforgery();
        app.MapStaticAssets();

        app.MapAccountForms();

        app.MapRazorComponents<
            Api.Components.Identity.AuthApp>();

        return app;
    }
}