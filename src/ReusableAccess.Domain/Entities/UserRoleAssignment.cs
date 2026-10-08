using System;

namespace ReusableAccess.Domain.Entities;

public sealed class UserRoleAssignment
{
    private UserRoleAssignment()
    {
    }

    public UserRoleAssignment(
        Guid userId,
        Guid roleId,
        Guid scopeId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ کاربر نباید خالی باشد.",
                nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ نقش نباید خالی باشد.",
                nameof(roleId));
        }

        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ محدوده نباید خالی باشد.",
                nameof(scopeId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        RoleId = roleId;
        ScopeId = scopeId;
        IsEnabled = true;
    }

    public Guid Id { get; private set; }

    // در پیاده‌سازی Identity، این شناسه به حساب همان کاربر اشاره می‌کند.
    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid ScopeId { get; private set; }

    // فعال‌بودن تخصیص؛ نه انتخاب‌شدن در نشست فعلی.
    public bool IsEnabled { get; private set; }

    public void Disable()
    {
        IsEnabled = false;
    }

    public void Enable()
    {
        IsEnabled = true;
    }
}