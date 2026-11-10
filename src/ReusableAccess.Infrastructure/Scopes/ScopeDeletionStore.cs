using System.Data;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Scopes;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Scopes;

public sealed class ScopeDeletionStore(
    ApplicationDbContext dbContext) : IScopeDeletionStore
{
    public async Task<ScopeDeletionStatus> DeleteAsync(
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        // بررسی وابستگی‌ها و حذف باید در یک مرز تراکنشی انجام شوند.
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var scopes = dbContext.Set<AccessScope>();

        var scope = await scopes.SingleOrDefaultAsync(
            item => item.Id == scopeId,
            cancellationToken);

        if (scope is null)
            return ScopeDeletionStatus.NotFound;

        var hasDependencies =
            await scopes.AnyAsync(
                item => item.ParentScopeId == scopeId,
                cancellationToken) ||
            await dbContext.Set<WorkItem>().AnyAsync(
                item => item.ScopeId == scopeId,
                cancellationToken) ||
            await dbContext.Set<UserRoleAssignment>().AnyAsync(
                item => item.ScopeId == scopeId,
                cancellationToken) ||
            await dbContext.Set<UserAccessBundleScope>().AnyAsync(
                item => item.ScopeId == scopeId,
                cancellationToken);

        if (hasDependencies)
            return ScopeDeletionStatus.HasDependencies;

        scopes.Remove(scope);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ScopeDeletionStatus.Deleted;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);

            return ScopeDeletionStatus.ConcurrencyConflict;
        }

        // خطاهای ناشناخته و لغو درخواست عمداً پنهان نمی‌شوند.
    }
}