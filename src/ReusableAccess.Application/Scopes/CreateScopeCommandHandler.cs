using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed class CreateScopeCommandHandler(
    IScopeCommandStore commandStore)
    : IRequestHandler<CreateScopeCommand, ScopeCreationResult>
{
    public Task<ScopeCreationResult> Handle(
        CreateScopeCommand request,
        CancellationToken cancellationToken)
    {
        return commandStore.CreateAsync(request, cancellationToken);
    }
}