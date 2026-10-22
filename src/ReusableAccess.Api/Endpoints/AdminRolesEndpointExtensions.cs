using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class AdminRolesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminRolesEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/roles",
            async (
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var roles = await dbContext.Roles
                    .AsNoTracking()
                    .OrderBy(role => role.Code)
                    .Select(role => new
                    {
                        roleId = role.Id,
                        roleCode = role.Code,
                        displayName = role.DisplayName
                    })
                    .ToListAsync(cancellationToken);

                return Results.Ok(new
                {
                    data = roles,
                    message = "فهرست نقش‌ها."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPost(
            "/api/admin/roles",
            async (
                CreateRoleRequest request,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var code = request.Code?.Trim();
                var displayName = request.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(code)
                    || code.Length > 100
                    || string.IsNullOrWhiteSpace(displayName)
                    || displayName.Length > 200)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "کد نقش و نام نمایشی الزامی هستند؛ حداکثر طول آن‌ها به‌ترتیب ۱۰۰ و ۲۰۰ کاراکتر است."
                    });
                }

                var codeExists = await dbContext.Roles
                    .AnyAsync(role => role.Code == code, cancellationToken);

                if (codeExists)
                {
                    return Results.Conflict(new
                    {
                        message = "این کد نقش قبلاً ثبت شده است."
                    });
                }

                var role = new Role(code, displayName);

                dbContext.Roles.Add(role);
                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Created(
                    $"/api/admin/roles/{role.Id}",
                    new
                    {
                        data = new
                        {
                            roleId = role.Id,
                            roleCode = role.Code,
                            displayName = role.DisplayName
                        },
                        message = "نقش ایجاد شد."
                    });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
            "/api/admin/roles/{roleId:guid}",
            async (
                Guid roleId,
                UpdateRoleRequest request,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var displayName = request.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(displayName)
                    || displayName.Length > 200)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "نام نمایشی نقش الزامی است و نباید بیشتر از ۲۰۰ کاراکتر باشد."
                    });
                }

                var role = await dbContext.Roles
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

                role.Rename(displayName);
                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Ok(new
                {
                    data = new
                    {
                        roleId = role.Id,
                        roleCode = role.Code,
                        displayName = role.DisplayName
                    },
                    message = "نقش به‌روزرسانی شد."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }
}

public sealed record CreateRoleRequest(
    string? Code,
    string? DisplayName);

public sealed record UpdateRoleRequest(
    string? DisplayName);