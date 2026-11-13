using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Bundles;

public interface IUserBundleRepository
{
    Task<bool> UserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> CountExistingRolesAsync(
        Guid[] roleIds,
        CancellationToken cancellationToken);

    Task<int> CountExistingScopesAsync(
        Guid[] scopeIds,
        CancellationToken cancellationToken);

    Task<UserAccessBundle?> FindForUpdateAsync(
        Guid bundleId,
        CancellationToken cancellationToken);

    void Add(UserAccessBundle bundle);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}