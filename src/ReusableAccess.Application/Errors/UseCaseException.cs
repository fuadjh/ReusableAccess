namespace ReusableAccess.Application.Errors;

// شکل قرارداد خطای قبلی حفظ می‌شود؛ یک Result سراسری جدید تحمیل نمی‌کنیم.
public sealed class UseCaseException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// ترجمهٔ concurrency فقط برای سناریوهای دارای همین قرارداد فعال می‌شود.
public interface IConcurrencyConflictRequest { }
