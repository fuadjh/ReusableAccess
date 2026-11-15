using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
namespace ReusableAccess.Application.Bundles;
public interface IUserAccessAssignmentRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);
    Task<Role?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken);
    Task<AccessScope?> FindScopeAsync(Guid scopeId, CancellationToken cancellationToken);
    Task<bool> HasBundlesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserRoleAssignment>> LoadEnabledLegacyAssignmentsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccessBundle>> LoadBundlesForUpdateAsync(Guid userId, CancellationToken cancellationToken);
    void Add(UserAccessBundle bundle);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public sealed record AssignUserAccessCommand(Guid ActorId, Guid UserId, Guid RoleId, Guid ScopeId)
    : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed class AssignUserAccessCommandHandler(IUserAccessAssignmentRepository repository, IWriteTransactionFactory transactions)
    : IRequestHandler<AssignUserAccessCommand, Guid>
{
    public async Task<Guid> Handle(AssignUserAccessCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var tx = await transactions.BeginSerializableAsync(cancellationToken);
        if (!await repository.UserExistsAsync(request.UserId, cancellationToken))
            throw new UseCaseException(404, "کاربر پیدا نشد.");
        var role = await repository.FindRoleAsync(request.RoleId, cancellationToken)
            ?? throw new UseCaseException(404, "نقش پیدا نشد.");
        var scope = await repository.FindScopeAsync(request.ScopeId, cancellationToken)
            ?? throw new UseCaseException(404, "محدوده پیدا نشد.");
        if (!await repository.HasBundlesAsync(request.UserId, cancellationToken))
        {
            // انتقال legacy پیش از اولین Bundle انجام می‌شود؛ ترتیب آن بخشی از رفتار موجود است.
            var pairs = await repository.LoadEnabledLegacyAssignmentsAsync(request.UserId, cancellationToken);
            foreach (var pair in pairs)
                repository.Add(new UserAccessBundle(request.UserId, "تخصیص منتقل‌شده", [pair.RoleId],
                    ScopeSelectionMode.Selected, [pair.ScopeId]));
            await repository.SaveChangesAsync(cancellationToken);
        }
        var bundles = await repository.LoadBundlesForUpdateAsync(request.UserId, cancellationToken);
        var match = bundles.FirstOrDefault(b => b.ScopeSelectionMode == ScopeSelectionMode.Selected &&
            b.Roles.Count == 1 && b.Scopes.Count == 1 && b.Roles.Any(r => r.RoleId == request.RoleId) &&
            b.Scopes.Any(s => s.ScopeId == request.ScopeId));
        if (match is null)
        {
            var name = $"{role.Code} / {scope.Code}";
            match = new UserAccessBundle(request.UserId, name[..Math.Min(name.Length, 200)],
                [request.RoleId], ScopeSelectionMode.Selected, [request.ScopeId]);
            repository.Add(match);
        }
        else match.Enable();
        await repository.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return match.Id;
    }
}
