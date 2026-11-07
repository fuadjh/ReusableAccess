using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using ReusableAccess.Web.Infrastructure;

namespace ReusableAccess.Web.Ui;

public static class UiEndpoints
{
    public static void MapReusableAccessUiEndpoints(this WebApplication app)
    {
        app.MapPost("/ui/access-profile/select",
            async Task<IResult> (
                HttpContext context,
                IAntiforgery antiforgery,
                AccessProfileSelectionService selectionService,
                CancellationToken ct) =>
            {
                context.Response.Headers.CacheControl = "no-store";

                if (!context.Request.HasFormContentType)
                    return Results.BadRequest("نوع درخواست معتبر نیست.");

                IFormCollection form;
                try
                {
                    await antiforgery.ValidateRequestAsync(context);
                    form = await context.Request.ReadFormAsync(ct);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest(
                        "اعتبار درخواست پایان یافته است؛ صفحه را تازه‌سازی کنید.");
                }
                catch (InvalidDataException)
                {
                    return Results.BadRequest("فرم درخواست معتبر نیست.");
                }

                var role = form["roleCode"].ToString();
                var mode = form["scopeMode"].ToString().ToLowerInvariant();
                var rawScope = form["scopeCode"].ToString();
                var scope = string.IsNullOrWhiteSpace(rawScope) ? null : rawScope;

                if (!Guid.TryParse(form["bundleId"].ToString(), out var bundleId) ||
                    bundleId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(role) || role.Length > 100 ||
                    (scope?.Length ?? 0) > 100 ||
                    mode is not ("selected" or "all") ||
                    (mode == "selected" && scope is null) ||
                    (mode == "all" && scope is not null))
                {
                    return Results.BadRequest("اطلاعات دسترسی انتخاب‌شده معتبر نیست.");
                }

                // همین نوع در Program.cs پروژه تعریف شده است؛ دوباره تعریفش نکنید.
                var request = new global::AccessProfileSelectionRequest(
                    bundleId, role, scope, mode);

                var result = await selectionService.SelectAsync(context, request, ct);
                var status = (result as IStatusCodeHttpResult)?.StatusCode;

                // بارگذاری کامل، منوها و policyهای circuit قبلی را هم تازه می‌کند.
                return status switch
                {
                    >= 200 and < 300 => Results.LocalRedirect(
                        "/console?ra_notice=profile-selected"),
                    401 => Results.LocalRedirect(
                        "/account/login?returnUrl=/console"),
                    403 => Results.LocalRedirect(
                        "/console?ra_notice=profile-denied"),
                    _ => Results.LocalRedirect(
                        "/console?ra_notice=profile-failed")
                };
            })
            .RequireAuthorization();
    }
}