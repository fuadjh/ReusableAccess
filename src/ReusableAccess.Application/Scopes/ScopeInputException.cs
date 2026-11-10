namespace ReusableAccess.Application.Scopes;

/// <summary>
/// خطای ورودی این قابلیت است؛ هیچ وابستگی‌ای به HTTP یا MVC ندارد.
/// تبدیل آن به پاسخ 400 وظیفهٔ مرز API است.
/// </summary>
public sealed class ScopeInputException(string message) : Exception(message)
{
}

internal static class ScopeCommandValidation
{
    public static CreateScopeCommand Normalize(CreateScopeCommand command)
    {
        var code = command.Code?.Trim() ?? string.Empty;
        var displayName = command.DisplayName?.Trim() ?? string.Empty;

        if (code.Length == 0 ||
            code.Length > 100 ||
            displayName.Length == 0 ||
            displayName.Length > 200 ||
            command.ParentScopeId == Guid.Empty)
        {
            throw new ScopeInputException(
                "کد و نام نمایشی الزامی‌اند؛ طول کد حداکثر ۱۰۰ و نام حداکثر ۲۰۰ کاراکتر است.");
        }

        // Snapshot نرمال‌شده؛ خود Request اولیه را تغییر نمی‌دهیم.
        return command with
        {
            Code = code,
            DisplayName = displayName
        };
    }

    public static string NormalizeDisplayName(string? value)
    {
        var displayName = value?.Trim() ?? string.Empty;

        if (displayName.Length == 0 || displayName.Length > 200)
        {
            throw new ScopeInputException(
                "نام نمایشی Scope الزامی است و حداکثر ۲۰۰ کاراکتر دارد.");
        }

        return displayName;
    }
}