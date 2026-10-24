using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed record GetUserBundlesQuery(Guid UserId)
    : IRequest<IReadOnlyList<AdminBundleDetails>?>;

public sealed record AdminBundleDetails(
    Guid BundleId,
    string Name,
    int SortOrder,
    bool IsEnabled,
    string ScopeMode,
    Guid[] RoleIds,
    Guid[] ScopeIds);

public interface IUserBundlesQueryStore
{
    Task<IReadOnlyList<AdminBundleDetails>?> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}