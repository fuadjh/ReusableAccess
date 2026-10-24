using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed record UpdateUserBundleCommand(
    Guid BundleId,
    string? Name,
    Guid[]? RoleIds,
    string? ScopeMode,
    Guid[]? ScopeIds,
    int SortOrder,
    bool IsEnabled)
    : IRequest<BundleUpdateResult>;

public enum BundleUpdateStatus
{
    Updated,
    BundleNotFound,
    InvalidRequest
}

public sealed record BundleUpdateResult(
    BundleUpdateStatus Status,
    string? ErrorMessage = null);