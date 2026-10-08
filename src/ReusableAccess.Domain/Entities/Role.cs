namespace ReusableAccess.Domain.Entities;

public sealed class Role
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    public Role(string code, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = Guid.NewGuid();
        Code = code.Trim();
        DisplayName = displayName.Trim();
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public IReadOnlyCollection<RolePermission> Permissions =>
        _permissions.AsReadOnly();

    public bool AddPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var alreadyAssigned = _permissions.Any(
            item => item.PermissionId == permission.Id);

        if (alreadyAssigned)
        {
            return false;
        }

        _permissions.Add(new RolePermission(Id, permission.Id));
        return true;
    }

    public bool RemovePermission(Guid permissionId)
    {
        var rolePermission = _permissions.FirstOrDefault(
            item => item.PermissionId == permissionId);

        if (rolePermission is null)
        {
            return false;
        }

        return _permissions.Remove(rolePermission);
    }
}