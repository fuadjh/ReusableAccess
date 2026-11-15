using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.AccessProfiles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.AccessProfiles;
public sealed class AccessProfilePreferenceRepository(ApplicationDbContext db) : IAccessProfilePreferenceRepository
{
    public Task<UserAccessBundle?> FindOwnedEnabledBundleAsync(Guid bundleId, Guid userId, CancellationToken cancellationToken)
        => db.Set<UserAccessBundle>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == bundleId && b.UserId == userId && b.IsEnabled, cancellationToken);
    public Task<Guid> FindRoleIdAsync(Guid bundleId, string roleCode, CancellationToken cancellationToken)
        => (from link in db.Set<UserAccessBundleRole>() join role in db.Roles on link.RoleId equals role.Id
            where link.BundleId == bundleId && role.Code == roleCode select role.Id).SingleOrDefaultAsync(cancellationToken);
    public Task<Guid?> FindScopeIdAsync(Guid bundleId, string? scopeCode, CancellationToken cancellationToken)
        => (from link in db.Set<UserAccessBundleScope>() join scope in db.Set<AccessScope>() on link.ScopeId equals scope.Id
            where link.BundleId == bundleId && scope.Code == scopeCode select (Guid?)scope.Id).SingleOrDefaultAsync(cancellationToken);
    public void SetSelection(Guid userId, AccessProfilePreferenceSelection selection)
    {
        var u = db.Users.Local.SingleOrDefault(u => u.Id == userId)
            ?? throw new InvalidOperationException("Load the account in the same scope first.");
        u.LastSelectedAccessBundleId = selection.BundleId;
        u.LastSelectedAccessRoleId = selection.RoleId;
        u.LastSelectedAccessScopeId = selection.ScopeId;
    }
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => _ = await db.SaveChangesAsync(cancellationToken);
}
