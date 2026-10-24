using MediatR;

namespace ReusableAccess.Application.Users;

public sealed record SearchUsersQuery(string Search)
    : IRequest<IReadOnlyList<AdminUserSearchResult>>;

public sealed record AdminUserSearchResult(
    Guid UserId,
    string? UserName,
    string DisplayName);

public interface IUserSearchStore
{
    Task<IReadOnlyList<AdminUserSearchResult>> SearchAsync(
        string search,
        CancellationToken cancellationToken);
}