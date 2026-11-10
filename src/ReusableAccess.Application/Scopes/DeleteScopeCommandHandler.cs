using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed class DeleteScopeCommandHandler(
    IScopeDeletionStore deletionStore)
    : IRequestHandler<DeleteScopeCommand, ScopeDeletionStatus>
{
    public Task<ScopeDeletionStatus> Handle(
        DeleteScopeCommand request,
        CancellationToken cancellationToken)
    {
        return deletionStore.DeleteAsync(
            request.ScopeId,
            cancellationToken);
    }
}