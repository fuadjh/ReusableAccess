using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed class RenameScopeCommandHandler(
    IScopeCommandStore commandStore)
    : IRequestHandler<RenameScopeCommand, ScopeRenameResult?>
{
    public Task<ScopeRenameResult?> Handle(
        RenameScopeCommand request,
        CancellationToken cancellationToken)
    {
        var displayName =
            ScopeCommandValidation.NormalizeDisplayName(request.DisplayName);

        return commandStore.RenameAsync(
            request.ScopeId,
            displayName,
            cancellationToken);
    }
}