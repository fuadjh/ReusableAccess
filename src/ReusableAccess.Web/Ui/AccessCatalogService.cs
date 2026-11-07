using System.ComponentModel.DataAnnotations;
using ReusableAccess.Contracts;
using ReusableAccess.Web.Completion;

namespace ReusableAccess.Web.Ui;

public enum CatalogKind { Roles, Scopes }

public sealed record CatalogItem(
    Guid Id, string Code, string Name, Guid? ParentScopeId = null);

public sealed class CatalogEditorModel
{
    [Required(ErrorMessage = "کد الزامی است.")]
    [StringLength(100, ErrorMessage = "کد حداکثر ۱۰۰ کاراکتر است.")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "عنوان الزامی است.")]
    [StringLength(200, ErrorMessage = "عنوان حداکثر ۲۰۰ کاراکتر است.")]
    public string DisplayName { get; set; } = "";

    public Guid? ParentScopeId { get; set; }
}

public sealed class AccessCatalogService(ConsoleApi api)
{
    private static string Path(CatalogKind kind) => kind switch
    {
        CatalogKind.Roles => "api/admin/roles",
        CatalogKind.Scopes => "api/admin/scopes",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public async Task<List<CatalogItem>> ListAsync(CatalogKind kind)
    {
        if (kind == CatalogKind.Roles)
        {
            var rows = await api.GetAsync<List<RoleRow>>(Path(kind));
            return rows.Select(x =>
                new CatalogItem(x.RoleId, x.RoleCode, x.DisplayName)).ToList();
        }

        var scopes = await api.GetAsync<List<ScopeRow>>(Path(kind));
        return scopes.Select(x => new CatalogItem(
            x.ScopeId, x.ScopeCode, x.DisplayName, x.ParentScopeId)).ToList();
    }

    public Task SaveAsync(
        CatalogKind kind, Guid? id, CatalogEditorModel model)
    {
        model.Code = (model.Code ?? "").Trim();
        model.DisplayName = (model.DisplayName ?? "").Trim();
        Validator.ValidateObject(model, new ValidationContext(model), true);

        // شناسه و کد پس از ایجاد تغییر نمی‌کنند؛ قرارداد API را گسترش نمی‌دهیم.
        if (id.HasValue)
            return api.WriteAsync(HttpMethod.Put,
                $"{Path(kind)}/{id.Value:D}",
                new { displayName = model.DisplayName });

        object body = kind == CatalogKind.Scopes
            ? new
            {
                code = model.Code,
                displayName = model.DisplayName,
                parentScopeId = model.ParentScopeId
            }
            : new
            {
                code = model.Code,
                displayName = model.DisplayName
            };

        return api.WriteAsync(HttpMethod.Post, Path(kind), body);
    }

    public Task DeleteAsync(CatalogKind kind, Guid id) =>
        api.WriteAsync(HttpMethod.Delete, $"{Path(kind)}/{id:D}");

    public Task<List<PermissionRow>> PermissionsAsync(Guid roleId) =>
        api.GetAsync<List<PermissionRow>>(
            $"api/admin/roles/{roleId:D}/permissions");

    public Task SavePermissionsAsync(Guid roleId, IEnumerable<string> keys) =>
        api.WriteAsync(HttpMethod.Put,
            $"api/admin/roles/{roleId:D}/permissions",
            new
            {
                permissionKeys = keys.Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray()
            });
}