namespace ReusableAccess.Domain.Entities;

public sealed class UserAccessBundleRole
{
    private UserAccessBundleRole()
    {
    }

    internal UserAccessBundleRole(Guid bundleId, Guid roleId)
    {
        if (bundleId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ بسته نباید خالی باشد.",
                nameof(bundleId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ نقش نباید خالی باشد.",
                nameof(roleId));
        }

        BundleId = bundleId;
        RoleId = roleId;
    }

    public Guid BundleId { get; private set; }

    public Guid RoleId { get; private set; }
}