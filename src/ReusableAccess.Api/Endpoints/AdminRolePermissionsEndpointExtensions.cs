using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class AdminRolePermissionsEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminRolePermissionsEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/roles/{roleId:guid}/permissions",
            async (
                Guid roleId,
                string? q,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var roleExists = await dbContext.Set<Role>()
                    .AsNoTracking()
                    .AnyAsync(role => role.Id == roleId, cancellationToken);

                if (!roleExists)
                {
                    return Results.NotFound(new
                    {
                        message = "نقش پیدا نشد."
                    });
                }

                var assignedPermissionIds = await dbContext
                    .Set<RolePermission>()
                    .AsNoTracking()
                    .Where(item => item.RoleId == roleId)
                    .Select(item => item.PermissionId)
                    .ToListAsync(cancellationToken);

                var assignedIds = assignedPermissionIds.ToHashSet();

                var permissionQuery = dbContext.Set<Permission>()
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var search = q.Trim();

                    permissionQuery = permissionQuery.Where(permission =>
                        permission.Key.Contains(search)
                        || permission.Description.Contains(search));
                }

                var permissions = await permissionQuery
                    .OrderBy(permission => permission.Key)
                    .Select(permission => new
                    {
                        permission.Id,
                        permission.Key,
                        permission.Description
                    })
                    .ToListAsync(cancellationToken);

                var data = permissions
                    .Select(permission =>
                    {
                        var separator = permission.Key.IndexOf('.');
                        var category = separator > 0
                            ? permission.Key[..separator]
                            : "general";

                        return new
                        {
                            permissionId = permission.Id,
                            key = permission.Key,
                            description = permission.Description,
                            category,
                            isSelected = assignedIds.Contains(permission.Id)
                        };
                    })
                    .ToArray();

                return Results.Ok(new
                {
                    data,
                    message = "فهرست مجوزهای نقش."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
            "/api/admin/roles/{roleId:guid}/permissions",
            async (
                Guid roleId,
                ReplaceRolePermissionsRequest request,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                if (request.PermissionKeys is null
                    || request.PermissionKeys.Any(
                        string.IsNullOrWhiteSpace))
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "permissionKeys باید آرایه‌ای از کلیدهای معتبر باشد."
                    });
                }

                var requestedKeys = request.PermissionKeys
                    .Select(key => key.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var role = await dbContext.Set<Role>()
                    .Include(item => item.Permissions)
                    .SingleOrDefaultAsync(
                        item => item.Id == roleId,
                        cancellationToken);

                if (role is null)
                {
                    return Results.NotFound(new
                    {
                        message = "نقش پیدا نشد."
                    });
                }

                var permissions = await dbContext.Set<Permission>()
                    .Where(permission =>
                        requestedKeys.Contains(permission.Key))
                    .ToListAsync(cancellationToken);

                var permissionsByKey = permissions.ToDictionary(
                    permission => permission.Key,
                    StringComparer.Ordinal);

                var unknownKeys = requestedKeys
                    .Where(key => !permissionsByKey.ContainsKey(key))
                    .ToArray();

                if (unknownKeys.Length > 0)
                {
                    return Results.BadRequest(new
                    {
                        message = "یک یا چند Permission ناشناخته است.",
                        unknownPermissionKeys = unknownKeys
                    });
                }

                var desiredPermissionIds = permissions
                    .Select(permission => permission.Id)
                    .ToHashSet();

                foreach (var currentPermission in role.Permissions.ToArray())
                {
                    if (!desiredPermissionIds.Contains(
                            currentPermission.PermissionId))
                    {
                        role.RemovePermission(
                            currentPermission.PermissionId);
                    }
                }

                foreach (var permission in permissions)
                {
                    role.AddPermission(permission);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Ok(new
                {
                    data = new
                    {
                        roleId = role.Id,
                        permissionKeys = permissions
                            .Select(permission => permission.Key)
                            .OrderBy(key => key)
                            .ToArray()
                    },
                    message = "مجوزهای نقش به‌روزرسانی شد."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }
}

public sealed record ReplaceRolePermissionsRequest(
    string[]? PermissionKeys);