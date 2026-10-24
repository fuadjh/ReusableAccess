using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Bundles;

public sealed class UserBundlesQueryStore(
    ApplicationDbContext dbContext) : IUserBundlesQueryStore
{
    public async Task<IReadOnlyList<AdminBundleDetails>?> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userExists = await dbContext.Users
            .AnyAsync(user => user.Id == userId, cancellationToken);

        if (!userExists)
        {
            return null;
        }

        var bundles = await dbContext.Set<UserAccessBundle>()
            .AsNoTracking()
            .Where(bundle => bundle.UserId == userId)
            .Include(bundle => bundle.Roles)
            .Include(bundle => bundle.Scopes)
            .OrderBy(bundle => bundle.SortOrder)
            .ThenBy(bundle => bundle.Name)
            .ToListAsync(cancellationToken);

        return bundles.Select(bundle => new AdminBundleDetails(
            bundle.Id,
            bundle.Name,
            bundle.SortOrder,
            bundle.IsEnabled,
            bundle.ScopeSelectionMode == ScopeSelectionMode.All
                ? "all"
                : "selected",
            bundle.Roles.Select(role => role.RoleId).ToArray(),
            bundle.Scopes.Select(scope => scope.ScopeId).ToArray()))
            .ToArray();
    }
}