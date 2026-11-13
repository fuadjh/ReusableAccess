using MediatR;
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

        // ساخت موجودیت در Application هماهنگ می‌شود.
        // اعتبار و نرمال‌سازی داخلی همچنان مسئولیت Domain است.
        var role = new Role(
            request.Code,
            request.DisplayName);

        // بررسی تکرار بر اساس همان Code انجام می‌شود
        // که موجودیت قرار است ذخیره کند.
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

        await roles.SaveChangesAsync(
            cancellationToken);

        return new RoleCreationResult(
            AlreadyExists: false,
            RoleId: role.Id,
            RoleCode: role.Code,
            DisplayName: role.DisplayName);
    }
}