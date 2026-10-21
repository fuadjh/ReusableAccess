using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class WorkItemsEndpointExtensions
{
    private const string ActiveScopeHeader = "X-Active-Scope";
    private const string ActiveScopeModeHeader = "X-Active-Scope-Mode";

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
                var modeValue = context.Request.Headers[
                    ActiveScopeModeHeader].ToString();

                if (!TryParseScopeMode(modeValue, out var scopeMode))
                {
                    return Results.BadRequest(
                        "X-Active-Scope-Mode باید selected یا all باشد.");
                }

                var query = dbContext.Set<WorkItem>()
                    .AsNoTracking();

                if (scopeMode == ScopeSelectionMode.Selected)
                {
                    var scopeCode = context.Request.Headers[
                        ActiveScopeHeader].ToString();

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

                    query = query.Where(
                        item => item.ScopeId == scopeId.Value);
                }

                var items = await query
                    .OrderBy(item => item.Title)
                    .Select(item => new
                    {
                        item.Id,
                        item.Title,
                        item.Status
                    })
                    .ToListAsync(cancellationToken);

                var message = scopeMode == ScopeSelectionMode.All
                    ? "موارد کاری همهٔ محدوده‌های مجاز."
                    : "موارد کاری محدودهٔ فعال.";

                return Results.Ok(new
                {
                    data = items,
                    message
                });
            })
            .RequireAuthorization("Permission:workitems.read");

        return endpoints;
    }

    private static bool TryParseScopeMode(
        string? value,
        out ScopeSelectionMode scopeMode)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(
                value.Trim(),
                "selected",
                StringComparison.OrdinalIgnoreCase))
        {
            scopeMode = ScopeSelectionMode.Selected;
            return true;
        }

        if (string.Equals(
            value.Trim(),
            "all",
            StringComparison.OrdinalIgnoreCase))
        {
            scopeMode = ScopeSelectionMode.All;
            return true;
        }

        scopeMode = default;
        return false;
    }
}