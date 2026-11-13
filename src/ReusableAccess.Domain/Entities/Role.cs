namespace ReusableAccess.Domain.Entities;

public sealed class Role
{
    public const int CodeMaxLength = 100;
    public const int DisplayNameMaxLength = 200;

    private readonly List<RolePermission> _permissions = [];

    // برای بازسازی موجودیت توسط ORM نگه داشته می‌شود.
    private Role()
    {
    }

    public Role(string code, string displayName)
    {
        // ابتدا همهٔ ورودی‌ها بررسی می‌شوند؛ سپس وضعیت مقداردهی می‌شود.
        var normalizedCode = NormalizeCode(code);
        var normalizedDisplayName = NormalizeDisplayName(displayName);

        Id = Guid.NewGuid();
        Code = normalizedCode;
        DisplayName = normalizedDisplayName;
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

    public void Rename(string displayName)
    {
        // اگر اعتبارسنجی شکست بخورد، مقدار قبلی دست‌نخورده می‌ماند.
        DisplayName = NormalizeDisplayName(displayName);
    }

    private static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalized = code.Trim();

        if (normalized.Length > CodeMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(code),
                $"کد نقش نباید بیشتر از {CodeMaxLength} کاراکتر باشد.");
        }

        // سیاست فعلی حفظ می‌شود: فقط Trim، بدون تغییر بزرگی حروف.
        return normalized;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalized = displayName.Trim();

        if (normalized.Length > DisplayNameMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayName),
                "نام نمایشی نقش نباید بیشتر از ۲۰۰ کاراکتر باشد.");
        }

        return normalized;
    }
}