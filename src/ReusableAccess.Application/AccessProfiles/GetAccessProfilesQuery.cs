using MediatR;

namespace ReusableAccess.Application.AccessProfiles;

public sealed record GetAccessProfilesQuery(Guid UserId)
    : IRequest<AccessProfilesResult?>;

public sealed record AccessProfileItem(
    Guid BundleId,
    int SortOrder,
    string BundleName,
    Guid RoleId,
    string RoleCode,
    Guid? ScopeId,
    string? ScopeCode,
    string ScopeMode);

public sealed record AccessProfilesResult(
    IReadOnlyList<AccessProfileItem> Data,
    AccessProfileItem? SelectedProfile,
    string Message);

public sealed record AccessProfilesSnapshot(
    Guid? LastSelectedAccessBundleId,
    Guid? LastSelectedAccessRoleId,
    Guid? LastSelectedAccessScopeId,
    IReadOnlyList<AccessProfileItem> Profiles);

public interface IAccessProfilesReadStore
{
    Task<AccessProfilesSnapshot?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);
}