using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.Users;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class AdminBundlesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminBundlesEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
     "/api/admin/users",
     async (
         string? q,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var search = q?.Trim();

         if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
         {
             return Results.BadRequest(new
             {
                 message = "برای جست‌وجوی کاربر حداقل دو حرف وارد کنید."
             });
         }

         var users = await sender.Send(
             new SearchUsersQuery(search),
             cancellationToken);

         return Results.Ok(new
         {
             data = users.Select(user => new
             {
                 userId = user.UserId,
                 userName = user.UserName,
                 displayName = user.DisplayName
             }),
             message = "کاربران پیدا‌شده."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapGet(
     "/api/admin/users/{userId:guid}/bundles",
     async (
         Guid userId,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var bundles = await sender.Send(
             new GetUserBundlesQuery(userId),
             cancellationToken);

         if (bundles is null)
         {
             return Results.NotFound(new
             {
                 message = "کاربر پیدا نشد."
             });
         }

         return Results.Ok(new
         {
             data = bundles.Select(bundle => new
             {
                 bundleId = bundle.BundleId,
                 bundle.Name,
                 bundle.SortOrder,
                 bundle.IsEnabled,
                 scopeMode = bundle.ScopeMode,
                 roleIds = bundle.RoleIds,
                 scopeIds = bundle.ScopeIds
             }),
             message = "Bundleهای کاربر."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapPost(
     "/api/admin/users/{userId:guid}/bundles",
     async (
         Guid userId,
         SaveBundleRequest request,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var result = await sender.Send(
             new CreateUserBundleCommand(
                 userId,
                 request.Name,
                 request.RoleIds,
                 request.ScopeMode,
                 request.ScopeIds,
                 request.SortOrder,
                 request.IsEnabled),
             cancellationToken);

         if (result.Status == BundleCreationStatus.UserNotFound)
         {
             return Results.NotFound(new
             {
                 message = "کاربر پیدا نشد."
             });
         }

         if (result.Status == BundleCreationStatus.InvalidRequest)
         {
             return Results.BadRequest(new
             {
                 message = result.ErrorMessage
             });
         }

         return Results.Created(
             $"/api/admin/bundles/{result.BundleId}",
             new
             {
                 data = new { bundleId = result.BundleId },
                 message = "Bundle ایجاد شد."
             });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
    "/api/admin/bundles/{bundleId:guid}",
    async (
        Guid bundleId,
        SaveBundleRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
    {
        var result = await sender.Send(
            new UpdateUserBundleCommand(
                bundleId,
                request.Name,
                request.RoleIds,
                request.ScopeMode,
                request.ScopeIds,
                request.SortOrder,
                request.IsEnabled),
            cancellationToken);

        if (result.Status == BundleUpdateStatus.BundleNotFound)
        {
            return Results.NotFound(new
            {
                message = "Bundle پیدا نشد."
            });
        }

        if (result.Status == BundleUpdateStatus.InvalidRequest)
        {
            return Results.BadRequest(new
            {
                message = result.ErrorMessage
            });
        }

        return Results.Ok(new
        {
            data = new { bundleId },
            message = "Bundle به‌روزرسانی شد."
        });
    })
    .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }

    private static ScopeSelectionMode ParseMode(string? mode) =>
        string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase)
            ? ScopeSelectionMode.All
            : ScopeSelectionMode.Selected;

    private static async Task<string?> ValidateRequestAsync(
        SaveBundleRequest request,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || request.Name.Trim().Length > 200
            || request.SortOrder < 0
            || request.RoleIds is null
            || request.RoleIds.Length == 0
            || request.RoleIds.Any(id => id == Guid.Empty)
            || request.ScopeIds is null
            || request.ScopeIds.Any(id => id == Guid.Empty))
        {
            return "اطلاعات Bundle معتبر نیست.";
        }

        var allMode = string.Equals(
            request.ScopeMode,
            "all",
            StringComparison.OrdinalIgnoreCase);
        var selectedMode = string.Equals(
            request.ScopeMode,
            "selected",
            StringComparison.OrdinalIgnoreCase);

        if (!allMode && !selectedMode)
        {
            return "scopeMode باید all یا selected باشد.";
        }

        if (allMode && request.ScopeIds.Length > 0)
        {
            return "در حالت all نباید Scope مشخص ارسال شود.";
        }

        if (selectedMode && request.ScopeIds.Length == 0)
        {
            return "در حالت selected دست‌کم یک Scope لازم است.";
        }

        var roleIds = request.RoleIds.Distinct().ToArray();
        var scopeIds = request.ScopeIds.Distinct().ToArray();

        if (await db.Set<Role>().CountAsync(
                role => roleIds.Contains(role.Id), ct) != roleIds.Length)
        {
            return "یک یا چند Role پیدا نشد.";
        }

        if (await db.Set<AccessScope>().CountAsync(
                scope => scopeIds.Contains(scope.Id), ct) != scopeIds.Length)
        {
            return "یک یا چند Scope پیدا نشد.";
        }

        return null;
    }
}

public sealed record SaveBundleRequest(
    string? Name,
    Guid[]? RoleIds,
    string? ScopeMode,
    Guid[]? ScopeIds,
    int SortOrder,
    bool IsEnabled);