using System.ComponentModel.DataAnnotations;
using ReusableAccess.Contracts;

namespace ReusableAccess.Web.Completion;

// هر دو رابط از یک سرویس استفاده می‌کنند؛ قراردادهای خواندنِ قدیمی
// عمداً یکسان فرض نشده‌اند. توکن فقط در ConsoleApi سمت سرور است.
public sealed class BundleManagementService(ConsoleApi api)
{
    public Task<List<RoleRow>> RolesAsync() =>
        api.GetAsync<List<RoleRow>>("api/admin/roles");

    public Task<List<ScopeRow>> ScopesAsync() =>
        api.GetAsync<List<ScopeRow>>("api/admin/scopes");

    public async Task<List<UserRow>> SearchAsync(string query, bool legacy)
    {
        var encoded = Uri.EscapeDataString(query.Trim());
        if (legacy)
            return await api.GetAsync<List<UserRow>>($"api/admin/users?q={encoded}");

        var page = await api.GetAsync<PageResult<UserRow>>(
            $"api/admin/users/manage?q={encoded}&page=1");
        return page.Items.ToList();
    }

    public Task<UserRow> UserAsync(Guid id) =>
        api.GetAsync<UserRow>($"api/admin/users/{id:D}/details");

    public Task<List<BundleRow>> ListAsync(Guid userId, bool legacy) =>
        api.GetAsync<List<BundleRow>>(
            $"api/admin/users/{userId:D}/{(legacy ? "bundles" : "access-list")}");

    public Task SaveAsync(Guid userId, Guid? bundleId, BundleEditorModel model)
    {
        if (userId == Guid.Empty || bundleId == Guid.Empty)
            throw new ValidationException("شناسهٔ کاربر یا بسته معتبر نیست.");

        model.Name = model.Name.Trim();
        Validator.ValidateObject(model, new ValidationContext(model), true);

        // Snapshot مانع وابستگی درخواست در حال اجرا به state قابل ویرایش UI است.
        var payload = new BundleWriteRequest(
            model.Name, model.RoleIds.OrderBy(x => x).ToArray(), model.ScopeMode,
            model.ScopeMode == "all" ? [] : model.ScopeIds.OrderBy(x => x).ToArray(),
            model.SortOrder, model.IsEnabled);

        return api.WriteAsync(
            bundleId.HasValue ? HttpMethod.Put : HttpMethod.Post,
            bundleId.HasValue
                ? $"api/admin/bundles/{bundleId.Value:D}"
                : $"api/admin/users/{userId:D}/bundles",
            payload);
    }

    public Task AssignAsync(Guid userId, Guid roleId, Guid scopeId)
    {
        if (userId == Guid.Empty || roleId == Guid.Empty || scopeId == Guid.Empty)
            throw new ValidationException("کاربر، نقش و محدوده را انتخاب کنید.");

        return api.WriteAsync(HttpMethod.Post,
            $"api/admin/users/{userId:D}/assign", new { roleId, scopeId });
    }

    private sealed record BundleWriteRequest(
        string Name, Guid[] RoleIds, string ScopeMode, Guid[] ScopeIds,
        int SortOrder, bool IsEnabled);
}
