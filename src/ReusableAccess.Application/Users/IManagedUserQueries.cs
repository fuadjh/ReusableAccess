using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Users;

public interface IManagedUserQueries
{
    Task<PageResult<UserRow>> SearchUsersAsync(
        string? query,
        int page,
        CancellationToken cancellationToken);

    Task<UserRow> CurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}