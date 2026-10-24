using MediatR;

namespace ReusableAccess.Application.Roles;

public sealed class CreateRoleCommandHandler(
    IRoleCommandStore commandStore)
    : IRequestHandler<CreateRoleCommand, RoleCreationResult>
{
    public Task<RoleCreationResult> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        return commandStore.CreateAsync(
            request.Code,
            request.DisplayName,
            cancellationToken);
    }
}