using Microsoft.EntityFrameworkCore;
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
                ApplicationDbContext db,
                CancellationToken ct) =>
            {
                var search = q?.Trim();

                if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
                {
                    return Results.BadRequest(new
                    {
                        message = "برای جست‌وجوی کاربر حداقل دو حرف وارد کنید."
                    });
                }

                var users = await db.Users
                    .AsNoTracking()
                    .Where(user =>
                        (user.UserName != null &&
                         user.UserName.Contains(search)) ||
                        user.DisplayName.Contains(search))
                    .OrderBy(user => user.DisplayName)
                    .Take(50)
                    .Select(user => new
                    {
                        userId = user.Id,
                        userName = user.UserName,
                        displayName = user.DisplayName
                    })
                    .ToListAsync(ct);

                return Results.Ok(new
                {
                    data = users,
                    message = "کاربران پیدا‌شده."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapGet(
            "/api/admin/users/{userId:guid}/bundles",
            async (
                Guid userId,
                ApplicationDbContext db,
                CancellationToken ct) =>
            {
                var userExists = await db.Users
                    .AnyAsync(user => user.Id == userId, ct);

                if (!userExists)
                {
                    return Results.NotFound(new
                    {
                        message = "کاربر پیدا نشد."
                    });
                }

                var bundles = await db.Set<UserAccessBundle>()
                    .AsNoTracking()
                    .Where(bundle => bundle.UserId == userId)
                    .Include(bundle => bundle.Roles)
                    .Include(bundle => bundle.Scopes)
                    .OrderBy(bundle => bundle.SortOrder)
                    .ThenBy(bundle => bundle.Name)
                    .ToListAsync(ct);

                return Results.Ok(new
                {
                    data = bundles.Select(bundle => new
                    {
                        bundleId = bundle.Id,
                        bundle.Name,
                        bundle.SortOrder,
                        bundle.IsEnabled,
                        scopeMode = bundle.ScopeSelectionMode ==
                                    ScopeSelectionMode.All
                            ? "all"
                            : "selected",
                        roleIds = bundle.Roles
                            .Select(role => role.RoleId)
                            .ToArray(),
                        scopeIds = bundle.Scopes
                            .Select(scope => scope.ScopeId)
                            .ToArray()
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
                ApplicationDbContext db,
                CancellationToken ct) =>
            {
                var userExists = await db.Users
                    .AnyAsync(user => user.Id == userId, ct);

                if (!userExists)
                {
                    return Results.NotFound(new { message = "کاربر پیدا نشد." });
                }

                var error = await ValidateRequestAsync(request, db, ct);
                if (error is not null)
                {
                    return Results.BadRequest(new { message = error });
                }

                var mode = ParseMode(request.ScopeMode);
                var bundle = new UserAccessBundle(
                    userId,
                    request.Name!.Trim(),
                    request.RoleIds!,
                    mode,
                    request.ScopeIds!,
                    request.SortOrder);

                db.Set<UserAccessBundle>().Add(bundle);
                await db.SaveChangesAsync(ct);

                return Results.Created(
                    $"/api/admin/bundles/{bundle.Id}",
                    new
                    {
                        data = new { bundleId = bundle.Id },
                        message = "Bundle ایجاد شد."
                    });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
            "/api/admin/bundles/{bundleId:guid}",
            async (
                Guid bundleId,
                SaveBundleRequest request,
                ApplicationDbContext db,
                CancellationToken ct) =>
            {
                var bundle = await db.Set<UserAccessBundle>()
                    .Include(item => item.Roles)
                    .Include(item => item.Scopes)
                    .SingleOrDefaultAsync(item => item.Id == bundleId, ct);

                if (bundle is null)
                {
                    return Results.NotFound(new { message = "Bundle پیدا نشد." });
                }

                var error = await ValidateRequestAsync(request, db, ct);
                if (error is not null)
                {
                    return Results.BadRequest(new { message = error });
                }

                bundle.Rename(request.Name!.Trim());
                bundle.ChangeSortOrder(request.SortOrder);
                bundle.ReplaceDefinition(
                    request.RoleIds!,
                    ParseMode(request.ScopeMode),
                    request.ScopeIds!);

                if (request.IsEnabled)
                {
                    bundle.Enable();
                }
                else
                {
                    bundle.Disable();
                }

                await db.SaveChangesAsync(ct);

                return Results.Ok(new
                {
                    data = new { bundleId = bundle.Id },
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