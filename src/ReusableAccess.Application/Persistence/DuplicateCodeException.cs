namespace ReusableAccess.Application.Persistence;

public enum CodeOwner
{
    Role,
    Scope
}

// خطای فنی شناخته‌شده، بدون وابستگی به EF یا SqlClient.
// تبدیل آن به نتیجهٔ Use Case وظیفهٔ Handler است.
public sealed class DuplicateCodeException(
    CodeOwner owner,
    string code,
    Exception? innerException = null)
    : Exception("A record with the same code already exists.", innerException)
{
    public CodeOwner Owner { get; } = owner;

    public string Code { get; } = code;
}