using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Application.Roles;
public interface IRoleDeletionRepository
{
    Task<Role?> FindForUpdateAsync(Guid roleId, CancellationToken cancellationToken);
    Task<bool> HasAssignmentsAsync(Guid roleId, CancellationToken cancellationToken);
    Task RemoveWithPermissionsAsync(Role role, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
// Kind فقط برای سازگاری با شکل قبلی قرارداد باقی است.
public sealed record DeleteAccessEntityCommand(Guid ActorId, string Kind, Guid Id)
    : IRequest<bool>, IConcurrencyConflictRequest;
public sealed class DeleteAccessEntityCommandHandler(IRoleDeletionRepository repository, IWriteTransactionFactory transactions)
    : IRequestHandler<DeleteAccessEntityCommand, bool>
{
    public async Task<bool> Handle(DeleteAccessEntityCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var tx = await transactions.BeginSerializableAsync(cancellationToken);
        if (request.Kind != "role") throw new UseCaseException(400, "نوع موجودیت معتبر نیست.");
        var role = await repository.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new UseCaseException(404, "نقش پیدا نشد.");
        if (await repository.HasAssignmentsAsync(role.Id, cancellationToken))
            throw new UseCaseException(409, "نقش دارای تخصیص است و قابل حذف نیست.");
        await repository.RemoveWithPermissionsAsync(role, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }
}
