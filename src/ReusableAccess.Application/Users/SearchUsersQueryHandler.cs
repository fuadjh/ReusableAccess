using MediatR;

namespace ReusableAccess.Application.Users;

public sealed class SearchUsersQueryHandler(
    IUserSearchStore store)
    : IRequestHandler<SearchUsersQuery, IReadOnlyList<AdminUserSearchResult>>
{
    public Task<IReadOnlyList<AdminUserSearchResult>> Handle(
        SearchUsersQuery request,
        CancellationToken cancellationToken)
    {
        return store.SearchAsync(request.Search, cancellationToken);
    }
}