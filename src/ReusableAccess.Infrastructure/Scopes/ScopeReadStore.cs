using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Scopes;

public sealed class ScopeReadStore(
    ApplicationDbContext dbContext) : IScopeReadStore
{
    public async Task<IReadOnlyList<ScopeListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<AccessScope>()
            .AsNoTracking()
            .OrderBy(scope => scope.Code)
            .Select(scope => new ScopeListItem(
                scope.Id,
                scope.Code,
                scope.DisplayName,
                scope.ParentScopeId))
            .ToListAsync(cancellationToken);
    }
}