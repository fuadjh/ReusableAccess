using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.AccessProfiles;

public sealed class AccessProfilesReadStore(
    ApplicationDbContext dbContext) : IAccessProfilesReadStore
{
    public async Task<AccessProfilesSnapshot?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new
            {
                item.LastSelectedAccessBundleId,
                item.LastSelectedAccessRoleId,
                item.LastSelectedAccessScopeId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var selectedScopeProfiles = await (
            from bundle in dbContext.Set<UserAccessBundle>()
            join bundleRole in dbContext.Set<UserAccessBundleRole>()
                on bundle.Id equals bundleRole.BundleId
            join role in dbContext.Set<Role>()
                on bundleRole.RoleId equals role.Id
            join bundleScope in dbContext.Set<UserAccessBundleScope>()
                on bundle.Id equals bundleScope.BundleId
            join scope in dbContext.Set<AccessScope>()
                on bundleScope.ScopeId equals scope.Id
            where bundle.UserId == userId
                  && bundle.IsEnabled
                  && bundle.ScopeSelectionMode == ScopeSelectionMode.Selected
            select new AccessProfileItem(
                bundle.Id,
                bundle.SortOrder,
                bundle.Name,
                role.Id,
                role.Code,
                scope.Id,
                scope.Code,
                "selected"))
            .ToListAsync(cancellationToken);

        var allScopeProfiles = await (
            from bundle in dbContext.Set<UserAccessBundle>()
            join bundleRole in dbContext.Set<UserAccessBundleRole>()
                on bundle.Id equals bundleRole.BundleId
            join role in dbContext.Set<Role>()
                on bundleRole.RoleId equals role.Id
            where bundle.UserId == userId
                  && bundle.IsEnabled
                  && bundle.ScopeSelectionMode == ScopeSelectionMode.All
            select new AccessProfileItem(
                bundle.Id,
                bundle.SortOrder,
                bundle.Name,
                role.Id,
                role.Code,
                null,
                null,
                "all"))
            .ToListAsync(cancellationToken);

        var profiles = selectedScopeProfiles
            .Concat(allScopeProfiles)
            .OrderBy(profile => profile.SortOrder)
            .ThenBy(profile => profile.BundleName)
            .ThenBy(profile => profile.RoleCode)
            .ThenBy(profile => profile.ScopeMode)
            .ThenBy(profile => profile.ScopeCode)
            .ToArray();

        return new AccessProfilesSnapshot(
            user.LastSelectedAccessBundleId,
            user.LastSelectedAccessRoleId,
            user.LastSelectedAccessScopeId,
            profiles);
    }
}