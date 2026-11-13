using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Scopes;

public sealed class ScopeDeletionRepository(
    ApplicationDbContext dbContext)
    : IScopeDeletionRepository
{
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

    public async Task<bool> HasDependenciesAsync(
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        return
            await dbContext.Set<AccessScope>().AnyAsync(
                scope => scope.ParentScopeId == scopeId,
                cancellationToken)
            ||
            await dbContext.Set<WorkItem>().AnyAsync(
                item => item.ScopeId == scopeId,
                cancellationToken)
            ||
            await dbContext.Set<UserRoleAssignment>().AnyAsync(
                assignment => assignment.ScopeId == scopeId,
                cancellationToken)
            ||
            await dbContext.Set<UserAccessBundleScope>().AnyAsync(
                item => item.ScopeId == scopeId,
                cancellationToken);
    }

    public async Task<bool> TryDeleteAsync(
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var trackedBeforeSave =
            PersistenceSaveChanges.CaptureTrackedEntities(dbContext);

        dbContext.Set<AccessScope>().Remove(scope);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException error) when (
            error.Entries.Count == 1
            && ReferenceEquals(error.Entries[0].Entity, scope))
        {
            PersistenceSaveChanges.DiscardFailedOperation(
                dbContext,
                trackedBeforeSave,
                scope);

            return false;
        }
    }
}