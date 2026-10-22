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
                var roles = await dbContext.Set<Role>()
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

        return endpoints;
    }
}