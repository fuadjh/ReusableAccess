using MediatR;
using ReusableAccess.Application.Persistence;
using System.Transactions;

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

public sealed class DeleteScopeCommandHandler(
    IScopeDeletionRepository repository,
    IWriteTransactionFactory transactions)
    : IRequestHandler<DeleteScopeCommand, ScopeDeletionStatus>
{
    public async Task<ScopeDeletionStatus> Handle(
        DeleteScopeCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await transactions.BeginSerializableAsync(cancellationToken);

        var scope = await repository.FindForUpdateAsync(
            request.ScopeId,
            cancellationToken);

        if (scope is null)
        {
            return ScopeDeletionStatus.NotFound;
        }

        var hasDependencies = await repository.HasDependenciesAsync(
            scope.Id,
            cancellationToken);

        if (hasDependencies)
        {
            return ScopeDeletionStatus.HasDependencies;
        }

        var deleted = await repository.TryDeleteAsync(
            scope,
            cancellationToken);

        if (!deleted)
        {
            // پاک‌سازی تراکنش را به Token لغوشده وابسته نمی‌کنیم.
            await transaction.RollbackAsync(CancellationToken.None);

            return ScopeDeletionStatus.ConcurrencyConflict;
        }

        await transaction.CommitAsync(cancellationToken);

        return ScopeDeletionStatus.Deleted;
    }
}