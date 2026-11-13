using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Scopes;

public interface IScopeRepository
{
    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(
        Guid scopeId,
        CancellationToken cancellationToken);

    Task<AccessScope?> FindForUpdateAsync(
        Guid scopeId,
        CancellationToken cancellationToken);

    void Add(AccessScope scope);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}