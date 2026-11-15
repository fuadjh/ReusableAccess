using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Application.AccessProfiles;
public interface IAccessProfilePreferenceRepository
{
    Task<UserAccessBundle?> FindOwnedEnabledBundleAsync(Guid bundleId, Guid userId, CancellationToken cancellationToken);
    Task<Guid> FindRoleIdAsync(Guid bundleId, string roleCode, CancellationToken cancellationToken);
    Task<Guid?> FindScopeIdAsync(Guid bundleId, string? scopeCode, CancellationToken cancellationToken);
    void SetSelection(Guid userId, AccessProfilePreferenceSelection selection);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
