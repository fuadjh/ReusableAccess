using ReusableAccess.Domain.Enums;

namespace ReusableAccess.Application.Bundles;

internal static class UserBundleValidation
{
    public static async Task<(ScopeSelectionMode Mode, string? Error)>
        ValidateAsync(
            string? name,
            Guid[]? roleIds,
            string? scopeMode,
            Guid[]? scopeIds,
            int sortOrder,
            IUserBundleRepository repository,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Trim().Length > 200
            || sortOrder < 0
            || roleIds is null
            || roleIds.Length == 0
            || roleIds.Any(id => id == Guid.Empty)
            || scopeIds is null
            || scopeIds.Any(id => id == Guid.Empty))
        {
            return (default, "اطلاعات Bundle معتبر نیست.");
        }

        var allMode = string.Equals(
            scopeMode,
            "all",
            StringComparison.OrdinalIgnoreCase);

        var selectedMode = string.Equals(
            scopeMode,
            "selected",
            StringComparison.OrdinalIgnoreCase);

        if (!allMode && !selectedMode)
        {
            return (default, "scopeMode باید all یا selected باشد.");
        }

        if (allMode && scopeIds.Length > 0)
        {
            return (default, "در حالت all نباید Scope مشخص ارسال شود.");
        }

        if (selectedMode && scopeIds.Length == 0)
        {
            return (default, "در حالت selected دست‌کم یک Scope لازم است.");
        }

        // تعداد شناسه‌های یکتا باید با تعداد رکوردهای موجود برابر باشد.
        var distinctRoleIds = roleIds.Distinct().ToArray();
        var distinctScopeIds = scopeIds.Distinct().ToArray();

        var existingRoleCount = await repository.CountExistingRolesAsync(
            distinctRoleIds,
            cancellationToken);

        if (existingRoleCount != distinctRoleIds.Length)
        {
            return (default, "یک یا چند Role پیدا نشد.");
        }

        var existingScopeCount = await repository.CountExistingScopesAsync(
            distinctScopeIds,
            cancellationToken);

        if (existingScopeCount != distinctScopeIds.Length)
        {
            return (default, "یک یا چند Scope پیدا نشد.");
        }

        return (
            allMode ? ScopeSelectionMode.All : ScopeSelectionMode.Selected,
            null);
    }
}