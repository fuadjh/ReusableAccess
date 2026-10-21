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

            if (!IsAssignedProfile(json.RootElement, selection))
            {
                return Results.StatusCode(
                    StatusCodes.Status403Forbidden);
            }

            activeProfileStore.Set(
                authentication.Properties,
                selection.BundleId,
                selection.RoleCode,
                selection.ScopeCode,
                selection.ScopeMode);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authentication.Principal,
                authentication.Properties);

            return Results.Ok(new
            {
                bundleId = selection.BundleId,
                roleCode = selection.RoleCode,
                scopeCode = selection.ScopeCode,
                scopeMode = selection.ScopeMode,
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
        AccessProfileSelectionRequest selection)
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
                !profile.TryGetProperty("bundleId", out var bundleElement) ||
                bundleElement.ValueKind != JsonValueKind.String ||
                !profile.TryGetProperty("roleCode", out var roleElement) ||
                roleElement.ValueKind != JsonValueKind.String ||
                !profile.TryGetProperty("scopeMode", out var modeElement) ||
                modeElement.ValueKind != JsonValueKind.String ||
                !profile.TryGetProperty("scopeCode", out var scopeElement))
            {
                throw new JsonException(
                    "ساختار یکی از پروفایل‌های پاسخ API معتبر نیست.");
            }

            var bundleId = bundleElement.GetGuid();
            var roleCode = roleElement.GetString();
            var scopeMode = modeElement.GetString();

            string? scopeCode = scopeElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => scopeElement.GetString(),
                _ => throw new JsonException(
                    "scopeCode باید رشته یا null باشد.")
            };

            if (bundleId == selection.BundleId &&
                string.Equals(
                    roleCode,
                    selection.RoleCode,
                    StringComparison.Ordinal) &&
                string.Equals(
                    scopeMode,
                    selection.ScopeMode,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    scopeCode,
                    selection.ScopeCode,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}