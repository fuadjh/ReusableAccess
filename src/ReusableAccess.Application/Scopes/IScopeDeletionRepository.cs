using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Scopes;

public interface IScopeDeletionRepository
{
    Task<AccessScope?> FindForUpdateAsync(
        Guid scopeId,
        CancellationToken cancellationToken);

    Task<bool> HasDependenciesAsync(
        Guid scopeId,
        CancellationToken cancellationToken);

    // false فقط به معنی تعارض هم‌زمانی شناخته‌شدهٔ همین حذف است.
    // سایر خطاها منتشر می‌شوند.
    Task<bool> TryDeleteAsync(
        AccessScope scope,
        CancellationToken cancellationToken);
}