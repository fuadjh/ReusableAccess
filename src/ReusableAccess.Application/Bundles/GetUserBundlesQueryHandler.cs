using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed class GetUserBundlesQueryHandler(
    IUserBundleQueries queries)
    : IRequestHandler<
        GetUserBundlesQuery,
        IReadOnlyList<AdminBundleDetails>?>
{
    public Task<IReadOnlyList<AdminBundleDetails>?> Handle(
        GetUserBundlesQuery request,
        CancellationToken cancellationToken)
    {
        return queries.GetForUserAsync(
            request.UserId,
            cancellationToken);
    }
}