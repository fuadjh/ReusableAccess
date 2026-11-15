using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
namespace ReusableAccess.Application.Bundles;
public interface IUserAccessListQueries { Task<IReadOnlyList<BundleRow>> ListAsync(Guid userId, CancellationToken cancellationToken); }
public sealed record GetUserAccessListQuery(Guid UserId) : IRequest<IReadOnlyList<BundleRow>>, IConcurrencyConflictRequest;
public sealed class GetUserAccessListQueryHandler(IUserAccessListQueries queries)
    : IRequestHandler<GetUserAccessListQuery, IReadOnlyList<BundleRow>>
{
    public Task<IReadOnlyList<BundleRow>> Handle(GetUserAccessListQuery request, CancellationToken cancellationToken)
        => queries.ListAsync(request.UserId, cancellationToken);
}
