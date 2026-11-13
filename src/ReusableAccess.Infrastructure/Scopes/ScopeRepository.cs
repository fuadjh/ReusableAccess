using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Scopes;

public sealed class ScopeRepository(ApplicationDbContext dbContext)
    : IScopeRepository
{
    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<AccessScope>().AnyAsync(
            scope => scope.Code == code,
            cancellationToken);
    }

    public Task<bool> ExistsByIdAsync(
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<AccessScope>().AnyAsync(
            scope => scope.Id == scopeId,
            cancellationToken);
    }

    public Task<AccessScope?> FindForUpdateAsync(
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<AccessScope>()
            .AsTracking()
            .SingleOrDefaultAsync(
                scope => scope.Id == scopeId,
                cancellationToken);
    }

    public void Add(AccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        dbContext.Set<AccessScope>().Add(scope);
    }

    public Task SaveChangesAsync(
      CancellationToken cancellationToken)
    {
        return PersistenceSaveChanges.SaveWithCodeConflictTranslationAsync(
            dbContext,
            cancellationToken);
    }
}