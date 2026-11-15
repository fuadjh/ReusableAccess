using ReusableAccess.Application.Accounts;
using ReusableAccess.Infrastructure.Identity;
namespace ReusableAccess.Infrastructure.Accounts;
internal static class AccountIdentityMapping
{
    public static AccountRecord Read(ApplicationUser user) => new()
    {
        Id = user.Id,
        NationalId = user.NationalId,
        UserName = user.UserName,
        FirstName = user.FirstName,
        LastName = user.LastName,
        DisplayName = user.DisplayName,
        OrganizationalCode = user.OrganizationalCode,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        EmailConfirmed = user.EmailConfirmed,
        PhoneNumberConfirmed = user.PhoneNumberConfirmed,
        IsActive = user.IsActive,
        IsPlatformSuperAdmin = user.IsPlatformSuperAdmin,
        LockoutEnabled = user.LockoutEnabled,
        MustChangePassword = user.MustChangePassword,
        SessionVersion = user.SessionVersion,
        ConcurrencyStamp = user.ConcurrencyStamp
    };
    public static void Apply(AccountRecord source, ApplicationUser target)
    {
        target.Id = source.Id;
        target.NationalId = source.NationalId;
        target.UserName = source.UserName;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.DisplayName = source.DisplayName;
        target.OrganizationalCode = source.OrganizationalCode;
        target.Email = source.Email;
        target.PhoneNumber = source.PhoneNumber;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PhoneNumberConfirmed = source.PhoneNumberConfirmed;
        target.IsActive = source.IsActive;
        target.IsPlatformSuperAdmin = source.IsPlatformSuperAdmin;
        target.LockoutEnabled = source.LockoutEnabled;
        target.MustChangePassword = source.MustChangePassword;
        target.SessionVersion = source.SessionVersion;
        if (source.ConcurrencyStamp is not null) target.ConcurrencyStamp = source.ConcurrencyStamp;
        // PasswordHash، SecurityStamp و ترجیح پروفایل از Snapshot ورودی کپی نمی‌شوند.
    }
}
