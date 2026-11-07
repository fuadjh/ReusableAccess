using ReusableAccess.Web.Infrastructure;

namespace ReusableAccess.Web.Ui;

public sealed record AccessProfileOption(
    Guid BundleId,
    string BundleName,
    Guid RoleId,
    string RoleCode,
    Guid? ScopeId,
    string? ScopeCode,
    string ScopeMode)
{
    public string ScopeLabel =>
        ScopeMode == "all" ? "همهٔ محدوده‌ها" : ScopeCode ?? "";

    public bool Matches(ActiveAccessProfile? current) =>
        current is not null &&
        BundleId == current.BundleId &&
        string.Equals(RoleCode, current.RoleCode, StringComparison.Ordinal) &&
        string.Equals(ScopeCode, current.ScopeCode, StringComparison.Ordinal) &&
        string.Equals(ScopeMode, current.ScopeMode,
            StringComparison.OrdinalIgnoreCase);
}