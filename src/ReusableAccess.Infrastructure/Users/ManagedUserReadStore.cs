using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Completion;
using ReusableAccess.Application.Users;
using ReusableAccess.Contracts;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Users;

public sealed class ManagedUserReadStore(ApplicationDbContext db)
    : IManagedUserReadStore
{
    private const int PageSize = 20;

    // Projection در دیتابیس انجام می‌شود و داده‌های محرمانه وارد DTO نمی‌شوند.
    private static readonly Expression<Func<ApplicationUser, UserRow>> Projection =
        user => new UserRow
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            OrganizationalCode = user.OrganizationalCode,
            NationalId = user.NationalId,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            Mobile = user.PhoneNumber ?? "",
            IsActive = user.IsActive,
            IsPlatformSuperAdmin = user.IsPlatformSuperAdmin,
            EmailConfirmed = user.EmailConfirmed,
            MobileConfirmed = user.PhoneNumberConfirmed,
            ConcurrencyStamp = user.ConcurrencyStamp ?? ""
        };

    public async Task<PageResult<UserRow>> SearchUsersAsync(
        string? query,
        int page,
        CancellationToken cancellationToken)
    {
        // جلوگیری از overflow در محاسبه Skip برای ورودی غیرعادی.
        page = Math.Clamp(page, 1, int.MaxValue / PageSize);

        var users = db.Users.AsNoTracking();
        var term = query?.Trim();

        if (!string.IsNullOrEmpty(term))
        {
            users = users.Where(user =>
                user.DisplayName.Contains(term) ||
                user.NationalId.Contains(term) ||
                user.OrganizationalCode.Contains(term));
        }

        var total = await users.CountAsync(cancellationToken);

        var items = await users
            .OrderBy(user => user.NationalId)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(Projection)
            .ToArrayAsync(cancellationToken);

        return new PageResult<UserRow>(
            items, total, page, PageSize);
    }

    public async Task<UserRow> CurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CompletionException(404, "کاربر پیدا نشد.");
}