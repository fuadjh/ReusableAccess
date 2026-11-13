using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed record RenameScopeCommand(
    Guid ScopeId,
    string DisplayName)
    : IRequest<ScopeRenameResult?>;

public sealed record ScopeRenameResult(
    Guid ScopeId,
    string ScopeCode,
    string DisplayName,
    Guid? ParentScopeId);

public sealed class RenameScopeCommandHandler(IScopeRepository repository)
    : IRequestHandler<RenameScopeCommand, ScopeRenameResult?>
{
    public async Task<ScopeRenameResult?> Handle(
        RenameScopeCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ورودی نامعتبر باید حتی برای شناسهٔ ناشناخته هم رد شود.
        var displayName =
            ScopeCommandValidation.NormalizeDisplayName(
                request.DisplayName);

        var scope = await repository.FindForUpdateAsync(
            request.ScopeId,
            cancellationToken);

        if (scope is null)
        {
            return null;
        }

        scope.Rename(displayName);

        await repository.SaveChangesAsync(cancellationToken);

        return new ScopeRenameResult(
            scope.Id,
            scope.Code,
            scope.DisplayName,
            scope.ParentScopeId);
    }
}