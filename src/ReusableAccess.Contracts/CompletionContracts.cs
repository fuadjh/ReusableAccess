using System.ComponentModel.DataAnnotations;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Contracts;

public sealed class NationalIdAttribute : ValidationAttribute
{
    public NationalIdAttribute()
    {
        ErrorMessage = "کد ملی معتبر نیست.";
    }

    public override bool IsValid(object? value)
        => IdentityRules.IsValidNationalId(value as string);
}

public class UserProfileInput
{
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [StringLength(100)]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "کد سازمانی الزامی است.")]
    [StringLength(50)]
    public string OrganizationalCode { get; set; } = "";

    [NationalId]
    public string NationalId { get; set; } = "";

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(30)]
    public string Mobile { get; set; } = "";

    public string UserName => IdentityRules.NormalizeDigits(NationalId);
}

public sealed class CreateUserInput : UserProfileInput
{
    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";
}

public sealed class UpdateUserInput : UserProfileInput
{
    public string ConcurrencyStamp { get; set; } = "";
}

public sealed class UserRow
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string OrganizationalCode { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Mobile { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsPlatformSuperAdmin { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool MobileConfirmed { get; set; }
    public string ConcurrencyStamp { get; set; } = "";
}

public sealed record PageResult<T>(
    IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record ActiveUserInput(
    bool IsActive, string ConcurrencyStamp);

public sealed record AdminPasswordInput(string Password);

public sealed record ChangePasswordInput(
    string CurrentPassword, string NewPassword);

public sealed record ContactRequest(DeliveryChannel Channel);
public sealed record ChallengeAnswer(Guid ChallengeId, string Code);
public sealed record ChallengeIssued(Guid ChallengeId);

public sealed record RoleRow(
    Guid RoleId, string RoleCode, string DisplayName);

public sealed record ScopeRow(
    Guid ScopeId, string ScopeCode,
    string DisplayName, Guid? ParentScopeId);

public sealed class PermissionRow
{
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsSelected { get; set; }
}

public sealed class BundleRow
{
    public Guid BundleId { get; set; }
    public string Name { get; set; } = "";
    public Guid[] RoleIds { get; set; } = [];
    public string ScopeMode { get; set; } = "selected";
    public Guid[] ScopeIds { get; set; } = [];
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class AuthSettingsInput
{
    public bool PublicRegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStartsUtc { get; set; }
    public DateTimeOffset? RegistrationEndsUtc { get; set; }
    public LoginMode LoginMode { get; set; }
    public bool RecoveryEnabled { get; set; }
    public DeliveryChannel RecoveryChannel { get; set; }
    public int Version { get; set; }
}

public sealed record SettingsView(
    AuthSettingsInput Policy,
    bool EmailConfigured,
    bool SmsConfigured);

public sealed record AuditRow(
    Guid Id, Guid? ActorId, DateTimeOffset OccurredUtc,
    string Action, string Entity, string EntityId,
    string ChangedFields);

public sealed record WorkItemRow(
    Guid Id, string Title, string Status);

public sealed record WorkItemInput(
    Guid ScopeId, string Title, string Status);

public sealed record ReportSummary(int Total, int Open, int Closed);

public sealed record PublicAuthOptions(
    bool RegistrationOpen,
    bool RecoveryEnabled,
    LoginMode LoginMode);

public sealed record LoginOutcome(
    bool Succeeded,
    Guid? ChallengeId = null,
    bool PasswordChangeRequired = false,
    string? Error = null);

public static class PermissionCatalog
{
    public static readonly IReadOnlyDictionary<string, string> All
        = new Dictionary<string, string>
        {
            ["diagnostics.read"] = "آزمایش دسترسی",
            ["workitems.read"] = "مشاهده موارد کاری",
            ["workitems.create"] = "ایجاد مورد کاری",
            ["workitems.update"] = "ویرایش مورد کاری",
            ["workitems.delete"] = "حذف مورد کاری",
            ["reports.read"] = "مشاهده گزارش‌ها"
        };
}