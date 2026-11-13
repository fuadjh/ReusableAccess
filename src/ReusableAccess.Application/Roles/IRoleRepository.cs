using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Roles;

/// <summary>
/// دسترسی موردنیاز عملیات نوشتن Role.
///
/// این قرارداد به EF Core، HTTP یا مدل پاسخ API وابسته نیست.
/// تصمیم‌های Use Case در Handler باقی می‌مانند.
/// </summary>
public interface IRoleRepository
{
    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// موجودیت را برای تغییر در همین واحد کار برمی‌گرداند.
    /// </summary>
    Task<Role?> FindForUpdateAsync(
        Guid roleId,
        CancellationToken cancellationToken);

    /// <summary>
    /// فقط موجودیت را برای درج ثبت می‌کند؛ ذخیره انجام نمی‌دهد.
    /// </summary>
    void Add(Role role);

    /// <summary>
    /// تغییرات واحد کار جاری را ذخیره می‌کند.
    /// زمان فراخوانی را Application تعیین می‌کند.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}