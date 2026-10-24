using MediatR;

namespace ReusableAccess.Application.Roles;

public sealed class RenameRoleCommandHandler(
    IRoleCommandStore commandStore)
    : IRequestHandler<RenameRoleCommand, RoleRenameResult?>
{
    public Task<RoleRenameResult?> Handle(
        RenameRoleCommand request,
        CancellationToken cancellationToken)
    {
        return commandStore.RenameAsync(
            request.RoleId,
            request.DisplayName,
            cancellationToken);
    }
}