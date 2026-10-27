using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.WorkItems;

public sealed class WorkItemsQueryStore(
    ApplicationDbContext dbContext) : IWorkItemsQueryStore
{
    public async Task<IReadOnlyList<WorkItemSummary>?> GetAsync(
     ScopeSelectionMode scopeMode,
     string? scopeCode,
     CancellationToken cancellationToken)
    {
        var query = dbContext.Set<WorkItem>()
            .AsNoTracking();

        if (scopeMode == ScopeSelectionMode.Selected)
        {
            var scopeId = await dbContext.Set<AccessScope>()
                .Where(scope => scope.Code == scopeCode)
                .Select(scope => (Guid?)scope.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (!scopeId.HasValue)
            {
                return null;
            }

            query = query.Where(item => item.ScopeId == scopeId.Value);
        }

        return await query
            .OrderBy(item => item.Title)
            .Select(item => new WorkItemSummary(
                item.Id,
                item.Title,
                item.Status))
            .ToListAsync(cancellationToken);
    }
}