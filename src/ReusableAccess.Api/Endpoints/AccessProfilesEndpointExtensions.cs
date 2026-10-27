using Microsoft.AspNetCore.Mvc;

using ReusableAccess.Domain.Enums;

using MediatR;
using ReusableAccess.Application.AccessProfiles;

namespace ReusableAccess.Api.Endpoints;

public static class AccessProfilesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAccessProfilesEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
     "/api/me/access-profiles",
    async (
    HttpContext context,
    [FromServices] ISender sender,
    CancellationToken cancellationToken) =>
     {
         var subject = context.User.FindFirst("sub")?.Value;
         if (!Guid.TryParse(subject, out var userId))
         {
             return Results.Forbid();
         }

         var result = await sender.Send(
             new GetAccessProfilesQuery(userId),
             cancellationToken);

         return result is null
             ? Results.Forbid()
             : Results.Ok(result);
     })
     .RequireAuthorization("AccessApi");

        endpoints.MapPost(
    "/api/me/access-profile-preference",
    async (
        HttpContext context,
        [FromServices] ISender sender,
        AccessProfilePreferenceRequest request,
        CancellationToken cancellationToken) =>
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Forbid();
        }

        var isSelectedMode = string.Equals(
            request.ScopeMode,
            "selected",
            StringComparison.OrdinalIgnoreCase);

        var isAllMode = string.Equals(
            request.ScopeMode,
            "all",
            StringComparison.OrdinalIgnoreCase);

        if (request.BundleId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.RoleCode)
            || (!isSelectedMode && !isAllMode)
            || (isSelectedMode && string.IsNullOrWhiteSpace(request.ScopeCode))
            || (isAllMode && !string.IsNullOrWhiteSpace(request.ScopeCode)))
        {
            return Results.BadRequest(
                "مشخصات پروفایل انتخاب‌شده معتبر نیست.");
        }

        var scopeMode = isSelectedMode
            ? ScopeSelectionMode.Selected
            : ScopeSelectionMode.All;

        var selection = await sender.Send(
            new SetAccessProfilePreferenceCommand(
                userId,
                request.BundleId,
                request.RoleCode,
                request.ScopeCode,
                scopeMode),
            cancellationToken);

        if (selection is null)
        {
            return Results.Forbid();
        }

        return Results.Ok(new
        {
            bundleId = selection.BundleId,
            roleId = selection.RoleId,
            scopeId = selection.ScopeId,
            message = "ترجیح پروفایل ذخیره شد."
        });
    })
    .RequireAuthorization("AccessApi");

        return endpoints;
    }
}

public sealed record AccessProfilePreferenceRequest(
    Guid BundleId,
    string RoleCode,
    string? ScopeCode,
    string ScopeMode);