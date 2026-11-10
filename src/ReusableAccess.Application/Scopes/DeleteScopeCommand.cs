using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed record DeleteScopeCommand(Guid ScopeId)
    : IRequest<ScopeDeletionStatus>;

public enum ScopeDeletionStatus
{
    Deleted,
    NotFound,
    HasDependencies,
    ConcurrencyConflict
}

/// <summary>
/// قرارداد عملیات اتمیک حذف؛ پیاده‌سازی دیتابیس در Infrastructure است.
/// </summary>
public interface IScopeDeletionStore
{
    Task<ScopeDeletionStatus> DeleteAsync(
        Guid scopeId,
        CancellationToken cancellationToken);
}