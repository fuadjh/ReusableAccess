using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class WorkItemsEndpointExtensions
{
    public static IEndpointRouteBuilder MapWorkItemsEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/work-items",
            async (
                HttpContext context,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var scopeCode =
                    context.Request.Headers["X-Active-Scope"].ToString();

                if (string.IsNullOrWhiteSpace(scopeCode))
                {
                    return Results.BadRequest(
                        "X-Active-Scope الزامی است.");
                }

                var scopeId = await dbContext.Set<AccessScope>()
                    .Where(scope => scope.Code == scopeCode)
                    .Select(scope => (Guid?)scope.Id)
                    .SingleOrDefaultAsync(cancellationToken);

                if (!scopeId.HasValue)
                {
                    return Results.Forbid();
                }

                var activeScopeId = scopeId.Value;
                var items = await dbContext.Set<WorkItem>()
                    .AsNoTracking()
                    .Where(item => item.ScopeId == activeScopeId)
                    .OrderBy(item => item.Title)
                    .Select(item => new
                    {
                        item.Id,
                        item.Title,
                        item.Status
                    })
                    .ToListAsync(cancellationToken);

                return Results.Ok(new
                {
                    data = items,
                    message = "موارد کاری محدودهٔ فعال."
                });
            })
            .RequireAuthorization("Permission:workitems.read");

        return endpoints;
    }
}