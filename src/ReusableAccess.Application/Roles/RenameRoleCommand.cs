using MediatR;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Roles;

public sealed record RenameRoleCommand(
    Guid RoleId,
    string DisplayName)
    : IRequest<RoleRenameResult?>;

public sealed record RoleRenameResult(
    Guid RoleId,
    string RoleCode,
    string DisplayName);

public sealed class RenameRoleCommandHandler(
    IRoleRepository roles)
    : IRequestHandler<RenameRoleCommand, RoleRenameResult?>
{
    public async Task<RoleRenameResult?> Handle(
        RenameRoleCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = await roles.FindForUpdateAsync(
            request.RoleId,
            cancellationToken);

        // قرارداد فعلی را حفظ می‌کنیم:
        // null یعنی Role موردنظر پیدا نشده است.
        if (role is null)
        {
            return null;
        }

        // تصمیم اجرای رفتار در Application است،
        // ولی قواعد تغییر نام داخل Domain اجرا می‌شوند.
        role.Rename(request.DisplayName);

        await roles.SaveChangesAsync(
            cancellationToken);

        return new RoleRenameResult(
            RoleId: role.Id,
            RoleCode: role.Code,
            DisplayName: role.DisplayName);
    }
}