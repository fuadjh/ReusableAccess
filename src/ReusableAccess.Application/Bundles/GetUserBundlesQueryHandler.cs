using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed class GetUserBundlesQueryHandler(
    IUserBundlesQueryStore store)
    : IRequestHandler<
        GetUserBundlesQuery,
        IReadOnlyList<AdminBundleDetails>?>
{
    public Task<IReadOnlyList<AdminBundleDetails>?> Handle(
        GetUserBundlesQuery request,
        CancellationToken cancellationToken)
    {
        return store.GetForUserAsync(
            request.UserId,
            cancellationToken);
    }
}