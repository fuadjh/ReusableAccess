using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.AccessProfiles;

public sealed class AccessProfilePreferenceStore(
    ApplicationDbContext dbContext) : IAccessProfilePreferenceStore
{
    public async Task<AccessProfilePreferenceSelection?> SaveAsync(
        SetAccessProfilePreferenceCommand command,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == command.UserId,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var bundle = await dbContext.Set<UserAccessBundle>()
            .SingleOrDefaultAsync(
                item => item.Id == command.BundleId
                        && item.UserId == command.UserId
                        && item.IsEnabled,
                cancellationToken);

        if (bundle is null || bundle.ScopeSelectionMode != command.ScopeMode)
        {
            return null;
        }

        var roleId = await (
            from bundleRole in dbContext.Set<UserAccessBundleRole>()
            join role in dbContext.Set<Role>()
                on bundleRole.RoleId equals role.Id
            where bundleRole.BundleId == bundle.Id
                  && role.Code == command.RoleCode
            select role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (roleId == Guid.Empty)
        {
            return null;
        }

        Guid? scopeId = null;

        if (command.ScopeMode == ScopeSelectionMode.Selected)
        {
            scopeId = await (
                from bundleScope in dbContext.Set<UserAccessBundleScope>()
                join scope in dbContext.Set<AccessScope>()
                    on bundleScope.ScopeId equals scope.Id
                where bundleScope.BundleId == bundle.Id
                      && scope.Code == command.ScopeCode
                select (Guid?)scope.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (scopeId is null)
            {
                return null;
            }
        }

        user.LastSelectedAccessBundleId = bundle.Id;
        user.LastSelectedAccessRoleId = roleId;
        user.LastSelectedAccessScopeId = scopeId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccessProfilePreferenceSelection(
            bundle.Id,
            roleId,
            scopeId);
    }
}