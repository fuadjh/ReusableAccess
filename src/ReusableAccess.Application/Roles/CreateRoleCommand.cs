using MediatR;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Roles;

public sealed record CreateRoleCommand(
    string Code,
    string DisplayName)
    : IRequest<RoleCreationResult>;

public sealed record RoleCreationResult(
    bool AlreadyExists,
    Guid? RoleId,
    string? RoleCode,
    string? DisplayName);

public sealed class CreateRoleCommandHandler(
    IRoleRepository roles)
    : IRequestHandler<CreateRoleCommand, RoleCreationResult>
{
    public async Task<RoleCreationResult> Handle(
     CreateRoleCommand request,
     CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = new Role(request.Code, request.DisplayName);

        var alreadyExists = await roles.ExistsByCodeAsync(
            role.Code,
            cancellationToken);

        if (alreadyExists)
        {
            return new RoleCreationResult(
                AlreadyExists: true,
                RoleId: null,
                RoleCode: null,
                DisplayName: null);
        }

        roles.Add(role);

        try
        {
            await roles.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateCodeException error) when (
            error.Owner == CodeOwner.Role
            && error.Code == role.Code)
        {
            // همان نتیجهٔ قبلی؛ مستقل از اینکه برخورد در Query دیده شده
            // یا پس از آن، هنگام ذخیره اتفاق افتاده است.
            return new RoleCreationResult(
                AlreadyExists: true,
                RoleId: null,
                RoleCode: null,
                DisplayName: null);
        }

        return new RoleCreationResult(
            AlreadyExists: false,
            RoleId: role.Id,
            RoleCode: role.Code,
            DisplayName: role.DisplayName);
    }
}