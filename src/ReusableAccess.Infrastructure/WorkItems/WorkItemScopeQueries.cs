using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.WorkItems;

public sealed class WorkItemScopeQueries(ApplicationDbContext db)
    : IWorkItemScopeQueries
{
    public Task<Guid?> FindIdByCodeAsync(
        string? code,
        CancellationToken cancellationToken)
    {
        return db.Set<AccessScope>()
            .AsNoTracking()
            .Where(scope => scope.Code == code)
            .Select(scope => (Guid?)scope.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        return db.Set<AccessScope>()
            .AnyAsync(
                scope => scope.Id == scopeId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ScopeRow>> ListAsync(
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        var query = db.Set<AccessScope>()
            .AsNoTracking();

        if (scopeId.HasValue)
        {
            query = query.Where(
                scope => scope.Id == scopeId.Value);
        }

        return await query
            .OrderBy(scope => scope.DisplayName)
            .Select(scope => new ScopeRow(
                scope.Id,
                scope.Code,
                scope.DisplayName,
                scope.ParentScopeId))
            .ToListAsync(cancellationToken);
    }
}