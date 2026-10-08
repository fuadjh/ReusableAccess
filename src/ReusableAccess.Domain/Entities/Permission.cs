namespace ReusableAccess.Domain.Entities;

public sealed class Permission
{
    // برای ساخت شیء توسط EF Core در مراحل بعدی
    private Permission()
    {
    }

    public Permission(string key, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Id = Guid.NewGuid();
        Key = key.Trim();
        Description = description.Trim();
    }

    public Guid Id { get; private set; }

    // کلید پایدار و قابل استفاده در کد، مانند "users.read"
    public string Key { get; private set; } = string.Empty;

    // توضیح قابل نمایش برای مدیر سامانه
    public string Description { get; private set; } = string.Empty;
}