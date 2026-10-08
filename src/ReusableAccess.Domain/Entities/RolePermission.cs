namespace ReusableAccess.Domain.Entities;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    internal RolePermission(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ نقش نباید خالی باشد.",
                nameof(roleId));
        }

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ مجوز نباید خالی باشد.",
                nameof(permissionId));
        }

        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }
}