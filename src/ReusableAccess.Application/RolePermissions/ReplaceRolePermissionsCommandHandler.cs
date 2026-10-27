using MediatR;

namespace ReusableAccess.Application.RolePermissions;

public sealed class ReplaceRolePermissionsCommandHandler(
    IRolePermissionsCommandStore store)
    : IRequestHandler<
        ReplaceRolePermissionsCommand,
        RolePermissionsReplacementResult>
{
    public Task<RolePermissionsReplacementResult> Handle(
        ReplaceRolePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        return store.ReplaceAsync(request, cancellationToken);
    }
}