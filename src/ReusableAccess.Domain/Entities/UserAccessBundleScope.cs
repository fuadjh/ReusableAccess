namespace ReusableAccess.Domain.Entities;

public sealed class UserAccessBundleScope
{
    private UserAccessBundleScope()
    {
    }

    internal UserAccessBundleScope(Guid bundleId, Guid scopeId)
    {
        if (bundleId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ بسته نباید خالی باشد.",
                nameof(bundleId));
        }

        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ اسکوپ نباید خالی باشد.",
                nameof(scopeId));
        }

        BundleId = bundleId;
        ScopeId = scopeId;
    }

    public Guid BundleId { get; private set; }

    public Guid ScopeId { get; private set; }
}