namespace ReusableAccess.Domain.Entities;

public sealed class AccessScope
{
    public const int CodeMaxLength = 100;
    public const int DisplayNameMaxLength = 200;
    private AccessScope() { }
    public AccessScope(string code, string displayName, Guid? parentScopeId = null)
    {
        var c = Normalize(code, CodeMaxLength, nameof(code));
        var n = Normalize(displayName, DisplayNameMaxLength, nameof(displayName));
        if (parentScopeId == Guid.Empty) throw new ArgumentException("شناسهٔ محدودهٔ والد نباید خالی باشد.", nameof(parentScopeId));
        Id = Guid.NewGuid(); Code = c; DisplayName = n; ParentScopeId = parentScopeId;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public Guid? ParentScopeId { get; private set; }
    public void Rename(string displayName) => DisplayName = Normalize(displayName, DisplayNameMaxLength, nameof(displayName));
    private static string Normalize(string value, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var result = value.Trim();
        if (result.Length > maximum) throw new ArgumentOutOfRangeException(parameter, $"طول مقدار نباید بیشتر از {maximum} کاراکتر باشد.");
        return result; // بزرگی حروف تغییر نمی‌کند.
    }
}
