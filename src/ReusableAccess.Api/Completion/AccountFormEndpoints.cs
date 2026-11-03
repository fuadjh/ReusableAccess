using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using OpenIddict.Server.AspNetCore;
using ReusableAccess.Api.Components.Identity;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;

namespace ReusableAccess.Api.Completion;

public static class AccountFormEndpoints
{
    public static string Local(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith('/') &&
           !value.StartsWith("//") &&
           !value.Contains('\\') &&
           !value.Any(char.IsControl)
            ? value : "/";

    private static IResult Return(LoginOutcome result, string? returnUrl)
    {
        var target = Local(returnUrl);

        if (result.Succeeded)
            return Results.LocalRedirect(target);

        if (result.ChallengeId.HasValue)
        {
            var page = result.PasswordChangeRequired
                ? "ChangeRequired" : "Challenge";

            return Results.LocalRedirect(
                $"/Account/{page}?id={result.ChallengeId}" +
                $"&returnUrl={Uri.EscapeDataString(target)}");
        }

        return Results.LocalRedirect(
            "/Account/Login?error=1&returnUrl=" +
            Uri.EscapeDataString(target));
    }

    public static void MapAccountForms(this WebApplication app)
    {
        var forms = app.MapGroup("/account/form")
            .AllowAnonymous()
            .RequireRateLimiting("account");

        forms.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                await context.HttpContext.RequestServices
                    .GetRequiredService<IAntiforgery>()
                    .ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(
                    "درخواست ضد-CSRF معتبر نیست؛ صفحه را تازه‌سازی کنید.");
            }

            try
            {
                return await next(context);
            }
            catch (CompletionException)
            {
                var form = await context.HttpContext.Request.ReadFormAsync();

                var page = context.HttpContext.Request.Path.Value?
                    .Split('/').Last() switch
                {
                    "register" => "Register",
                    "recovery" => "Recovery",
                    "reset" => "Reset",
                    "challenge" => "Challenge",
                    "change-required" => "ChangeRequired",
                    _ => "Login"
                };

                var id = Guid.TryParse(form["id"].ToString(), out var parsed)
                    ? parsed.ToString() : "";

                return Results.LocalRedirect(
                    $"/Account/{page}?error=1&id={id}&returnUrl=" +
                    Uri.EscapeDataString(Local(form["returnUrl"].ToString())));
            }
        });

        forms.MapPost("/password",
            async (HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                var f = await c.Request.ReadFormAsync(ct);

                return Return(await w.PasswordAsync(
                    f["nationalId"].ToString(),
                    f["password"].ToString(),
                    f["remember"].ToString() == "on", ct),
                    f["returnUrl"].ToString());
            });

        forms.MapPost("/challenge",
            async (HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                var f = await c.Request.ReadFormAsync(ct);
                var id = Guid.TryParse(f["id"].ToString(), out var parsed)
                    ? parsed : Guid.Empty;

                return Return(
                    await w.VerifyLoginAsync(id, f["code"].ToString(), ct),
                    f["returnUrl"].ToString());
            });

        forms.MapPost("/change-required",
            async (HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                var f = await c.Request.ReadFormAsync(ct);

                if (f["password"].ToString() != f["confirm"].ToString())
                    throw new CompletionException(400, "تکرار گذرواژه یکسان نیست.");

                var id = Guid.TryParse(f["id"].ToString(), out var parsed)
                    ? parsed : Guid.Empty;

                return Return(
                    await w.ChangeRequiredPasswordAsync(
                        id, f["password"].ToString(), ct),
                    f["returnUrl"].ToString());
            });

        forms.MapPost("/register",
            async (HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                var f = await c.Request.ReadFormAsync(ct);

                if (f["password"].ToString() != f["confirm"].ToString())
                    throw new CompletionException(400, "تکرار گذرواژه یکسان نیست.");

                await w.RegisterAsync(new CreateUserInput
                {
                    FirstName = f["firstName"].ToString(),
                    LastName = f["lastName"].ToString(),
                    OrganizationalCode = f["organizationalCode"].ToString(),
                    NationalId = f["nationalId"].ToString(),
                    Email = f["email"].ToString(),
                    Mobile = f["mobile"].ToString(),
                    Password = f["password"].ToString()
                }, ct);

                return Results.LocalRedirect(
                    "/Account/Login?done=registered&returnUrl=" +
                    Uri.EscapeDataString(Local(f["returnUrl"].ToString())));
            });

        forms.MapPost("/recovery",
            async (HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                var f = await c.Request.ReadFormAsync(ct);

                var id = await w.RequestRecoveryAsync(
                    f["nationalId"].ToString(), ct);

                return Results.LocalRedirect(
                    $"/Account/Reset?id={id}&returnUrl=" +
                    Uri.EscapeDataString(Local(f["returnUrl"].ToString())));
            });

        forms.MapPost("/reset",
            async (HttpContext c, IAccountWorkflow w, CancellationToken ct) =>
            {
                var f = await c.Request.ReadFormAsync(ct);

                if (f["password"].ToString() != f["confirm"].ToString())
                    throw new CompletionException(400, "تکرار گذرواژه یکسان نیست.");

                var id = Guid.TryParse(f["id"].ToString(), out var parsed)
                    ? parsed : Guid.Empty;

                await w.ResetRecoveredPasswordAsync(
                    id, f["code"].ToString(), f["password"].ToString(), ct);

                return Results.LocalRedirect(
                    "/Account/Login?done=reset&returnUrl=" +
                    Uri.EscapeDataString(Local(f["returnUrl"].ToString())));
            });

        app.MapGet("/connect/logout", (HttpContext c) =>
            (IResult)new RazorComponentResult<AuthLogoutDocument>(new
            {
                Action = $"{c.Request.PathBase}{c.Request.Path}{c.Request.QueryString}"
            })).AllowAnonymous();

        app.MapPost("/connect/logout",
            async (HttpContext c, IAntiforgery anti, IAccountWorkflow w) =>
            {
                try
                {
                    await anti.ValidateRequestAsync(c);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest("درخواست معتبر نیست.");
                }

                await w.SignOutAsync();

                return Results.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }).AllowAnonymous();
    }
}