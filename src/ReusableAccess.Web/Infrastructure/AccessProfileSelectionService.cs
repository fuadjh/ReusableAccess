using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ReusableAccess.Web.Infrastructure;

public sealed class AccessProfileSelectionService(
    BffApiClient bffApiClient,
    ActiveAccessProfileStore activeProfileStore)
{
    public async Task<IResult> SelectAsync(
        HttpContext context,
        AccessProfileSelectionRequest selection,
        CancellationToken cancellationToken)
    {
        var authentication = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authentication.Succeeded ||
            authentication.Principal is null ||
            authentication.Properties is null)
        {
            return Results.Unauthorized();
        }

        var accessToken = await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "توکن دسترسی در ticket سمت‌سرور موجود نیست.");
        }

        try
        {
            using var profilesResponse = await bffApiClient.GetAsync(
                "api/me/access-profiles",
                accessToken,
                activeProfile: null,
                cancellationToken);

            if (!profilesResponse.IsSuccessStatusCode)
            {
                return Results.StatusCode(
                    (int)profilesResponse.StatusCode);
            }

            await using var responseStream =
                await profilesResponse.Content.ReadAsStreamAsync(
                    cancellationToken);

            using var json = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: cancellationToken);

            if (!IsAssignedProfile(
                    json.RootElement,
                    selection.RoleCode,
                    selection.ScopeCode))
            {
                return Results.StatusCode(
                    StatusCodes.Status403Forbidden);
            }

            // پروفایل فقط پس از تأیید assignment در ticket ذخیره می‌شود.
            activeProfileStore.Set(
                authentication.Properties,
                selection.RoleCode,
                selection.ScopeCode);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authentication.Principal,
                authentication.Properties);

            return Results.Ok(new
            {
                roleCode = selection.RoleCode,
                scopeCode = selection.ScopeCode,
                message = "پروفایل فعال ذخیره شد."
            });
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "ارتباط با API برقرار نشد.");
        }
        catch (JsonException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "پاسخ فهرست پروفایل‌ها از API قابل خواندن نبود.");
        }
    }

    private static bool IsAssignedProfile(
        JsonElement root,
        string roleCode,
        string scopeCode)
    {
        if (!root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                "فیلد data در پاسخ API وجود ندارد یا آرایه نیست.");
        }

        foreach (var profile in data.EnumerateArray())
        {
            if (profile.ValueKind != JsonValueKind.Object ||
                !profile.TryGetProperty("roleCode", out var roleElement) ||
                !profile.TryGetProperty("scopeCode", out var scopeElement) ||
                roleElement.ValueKind != JsonValueKind.String ||
                scopeElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(
                    "ساختار یکی از پروفایل‌های پاسخ API معتبر نیست.");
            }

            if (string.Equals(
                    roleElement.GetString(),
                    roleCode,
                    StringComparison.Ordinal) &&
                string.Equals(
                    scopeElement.GetString(),
                    scopeCode,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}