namespace ReusableAccess.Application.Errors;

// شکل نتیجهٔ خطای قبلی حفظ می‌شود.
public sealed class UseCaseException(
    int statusCode,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// فقط درخواست‌هایی که قرارداد Conflict دارند، وارد ترجمهٔ مربوط می‌شوند.
public interface IConcurrencyConflictRequest
{
}