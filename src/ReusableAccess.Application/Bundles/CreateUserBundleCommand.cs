using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed record CreateUserBundleCommand(
    Guid UserId,
    string? Name,
    Guid[]? RoleIds,
    string? ScopeMode,
    Guid[]? ScopeIds,
    int SortOrder,
    bool IsEnabled)
    : IRequest<BundleCreationResult>;

public enum BundleCreationStatus
{
    Created,
    UserNotFound,
    InvalidRequest
}

public sealed record BundleCreationResult(
    BundleCreationStatus Status,
    Guid? BundleId = null,
    string? ErrorMessage = null);

public interface IUserBundleCommandStore
{
    Task<BundleCreationResult> CreateAsync(
        CreateUserBundleCommand command,
        CancellationToken cancellationToken);
    Task<BundleUpdateResult> UpdateAsync(
    UpdateUserBundleCommand command,
    CancellationToken cancellationToken);
}