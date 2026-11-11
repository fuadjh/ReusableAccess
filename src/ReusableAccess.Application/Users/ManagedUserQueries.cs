using MediatR;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Users;

public sealed record SearchUsers(string? Q, int Page = 1)
    : IRequest<PageResult<UserRow>>;

public sealed record GetCurrentUser(Guid UserId)
    : IRequest<UserRow>;

public sealed class SearchUsersHandler(IManagedUserReadStore store)
    : IRequestHandler<SearchUsers, PageResult<UserRow>>
{
    public Task<PageResult<UserRow>> Handle(
        SearchUsers request,
        CancellationToken cancellationToken)
        => store.SearchUsersAsync(
            request.Q,
            request.Page,
            cancellationToken);
}

public sealed class GetCurrentUserHandler(IManagedUserReadStore store)
    : IRequestHandler<GetCurrentUser, UserRow>
{
    public Task<UserRow> Handle(
        GetCurrentUser request,
        CancellationToken cancellationToken)
        => store.CurrentUserAsync(
            request.UserId,
            cancellationToken);
}