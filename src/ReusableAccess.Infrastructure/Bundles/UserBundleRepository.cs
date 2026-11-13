using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Bundles;

public sealed class UserBundleRepository(ApplicationDbContext dbContext)
    : IUserBundleRepository
{
    public Task<bool> UserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.Users.AnyAsync(
            user => user.Id == userId,
            cancellationToken);
    }

    public Task<int> CountExistingRolesAsync(
        Guid[] roleIds,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<Role>().CountAsync(
            role => roleIds.Contains(role.Id),
            cancellationToken);
    }

    public Task<int> CountExistingScopesAsync(
        Guid[] scopeIds,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<AccessScope>().CountAsync(
            scope => scopeIds.Contains(scope.Id),
            cancellationToken);
    }

    public Task<UserAccessBundle?> FindForUpdateAsync(
        Guid bundleId,
        CancellationToken cancellationToken)
    {
        // تعریف قبلی باید بارگذاری شود تا جایگزینی روابط درست انجام شود.
        return dbContext.Set<UserAccessBundle>()
            .AsTracking()
            .Include(bundle => bundle.Roles)
            .Include(bundle => bundle.Scopes)
            .SingleOrDefaultAsync(
                bundle => bundle.Id == bundleId,
                cancellationToken);
    }

    public void Add(UserAccessBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        dbContext.Set<UserAccessBundle>().Add(bundle);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}