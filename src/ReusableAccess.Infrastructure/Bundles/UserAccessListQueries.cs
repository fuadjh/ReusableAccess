using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Bundles;

public sealed class UserAccessListQueries(ApplicationDbContext db)
    : IUserAccessListQueries
{
    public async Task<IReadOnlyList<BundleRow>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var bundles = await db.Set<UserAccessBundle>()
            .AsNoTracking()
            .Include(bundle => bundle.Roles)
            .Include(bundle => bundle.Scopes)
            .Where(bundle => bundle.UserId == userId)
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync(cancellationToken);

        return bundles.Select(bundle => new BundleRow
        {
            BundleId = bundle.Id,
            Name = bundle.Name,
            SortOrder = bundle.SortOrder,
            IsEnabled = bundle.IsEnabled,

            ScopeMode =
                bundle.ScopeSelectionMode == ScopeSelectionMode.All
                    ? "all"
                    : "selected",

            RoleIds = bundle.Roles
                .Select(role => role.RoleId)
                .ToArray(),

            ScopeIds = bundle.Scopes
                .Select(scope => scope.ScopeId)
                .ToArray()
        }).ToArray();
    }
}