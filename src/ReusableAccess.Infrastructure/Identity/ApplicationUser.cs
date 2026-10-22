using Microsoft.AspNetCore.Identity;

namespace ReusableAccess.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string NationalId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsPlatformSuperAdmin { get; set; }
    // ترجیح آخرین پروفایل معتبر کاربر؛ مقدارها در API دوباره اعتبارسنجی می‌شوند.
    public Guid? LastSelectedAccessBundleId { get; set; }

    public Guid? LastSelectedAccessRoleId { get; set; }

    // در حالت All مقدار ScopeId خالی است.
    public Guid? LastSelectedAccessScopeId { get; set; }
}