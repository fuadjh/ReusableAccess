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
        var normalized = ScopeCommandValidation.Normalize(request);

        return commandStore.CreateAsync(
            normalized,
            cancellationToken);
    }
}