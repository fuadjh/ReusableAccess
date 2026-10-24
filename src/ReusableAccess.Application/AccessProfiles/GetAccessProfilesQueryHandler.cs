using MediatR;

namespace ReusableAccess.Application.AccessProfiles;

public sealed class GetAccessProfilesQueryHandler(
    IAccessProfilesReadStore readStore)
    : IRequestHandler<GetAccessProfilesQuery, AccessProfilesResult?>
{
    public async Task<AccessProfilesResult?> Handle(
        GetAccessProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await readStore.GetAsync(
            request.UserId,
            cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var selectedProfile = snapshot.Profiles.FirstOrDefault(profile =>
            profile.BundleId == snapshot.LastSelectedAccessBundleId
            && profile.RoleId == snapshot.LastSelectedAccessRoleId
            && profile.ScopeId == snapshot.LastSelectedAccessScopeId);

        selectedProfile ??= snapshot.Profiles.FirstOrDefault();

        return new AccessProfilesResult(
            snapshot.Profiles,
            selectedProfile,
            "بسته‌های دسترسی فعال کاربر.");
    }
}