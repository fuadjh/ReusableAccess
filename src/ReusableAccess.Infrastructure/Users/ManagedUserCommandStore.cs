using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Completion;
using ReusableAccess.Application.Users;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Identity;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Users;

public sealed class ManagedUserCommandStore(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider time)
    : IManagedUserCommandStore, IUserRegistrationStore
{
    private static void Check(IdentityResult result)
       => UserWriteConflict.Check(result);



    private async Task<ApplicationUser> User(Guid id, CancellationToken ct)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new CompletionException(404, "کاربر پیدا نشد.");

    
  
    private async Task<Guid> CreateUserAsync(
        CreateUserInput input, bool temporary, CancellationToken ct)
    {
        CompletionValidation.Profile(input);

        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        // POST مستقیم هم نباید بتواند بازهٔ ثبت‌نام را دور بزند.
        if (!temporary)
        {
            var policy = await db.Set<AuthenticationPolicy>()
                .SingleAsync(x => x.Id == 1, ct);

            if (!policy.IsRegistrationOpen(time.GetUtcNow()))
                throw new CompletionException(
                    403, "ثبت‌نام عمومی در این زمان فعال نیست.");
        }

        var national = IdentityRules.NormalizeDigits(input.NationalId);

        if (await db.Users.AnyAsync(x => x.NationalId == national, ct))
            throw new CompletionException(409, "کد ملی قبلاً ثبت شده است.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = national,
            NationalId = national,
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            DisplayName = $"{input.FirstName.Trim()} {input.LastName.Trim()}",
            OrganizationalCode = input.OrganizationalCode.Trim(),
            Email = input.Email.Trim(),
            PhoneNumber = IdentityRules.NormalizeMobile(input.Mobile),
            IsActive = true,
            IsPlatformSuperAdmin = false,
            LockoutEnabled = true,
            MustChangePassword = temporary
        };

        Check(await users.CreateAsync(user, input.Password));
        await tx.CommitAsync(ct);

        return user.Id;
    }

    private async Task<bool> UpdateCoreAsync(
        Guid actor, Guid id, UpdateUserInput input, CancellationToken ct)
    {
        CompletionValidation.Profile(input);
        var user = await User(id, ct);

        if (user.ConcurrencyStamp != input.ConcurrencyStamp)
            throw new CompletionException(
                409, "اطلاعات هم‌زمان تغییر کرده؛ دوباره دریافت کنید.");

        var national = IdentityRules.NormalizeDigits(input.NationalId);

        if (national != IdentityRules.NormalizeDigits(user.NationalId))
        {
            // فقط شناسهٔ نامعتبرِ دادهٔ قدیمی قابل اصلاح است.
            if (IdentityRules.IsValidNationalId(user.NationalId))
                throw new CompletionException(
                    400, "کد ملی معتبر پس از ایجاد قابل تغییر نیست.");

            if (await db.Users.AnyAsync(
                    x => x.Id != id && x.NationalId == national, ct))
                throw new CompletionException(409, "کد ملی قبلاً ثبت شده است.");

            user.NationalId = national;
            user.SessionVersion++;
        }

        if (user.UserName != national)
        {
            user.UserName = national;
            user.SessionVersion++;
        }

        user.FirstName = input.FirstName.Trim();
        user.LastName = input.LastName.Trim();
        user.DisplayName = $"{user.FirstName} {user.LastName}";
        user.OrganizationalCode = input.OrganizationalCode.Trim();

        var email = input.Email.Trim();
        var mobile = IdentityRules.NormalizeMobile(input.Mobile);

        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = email;
            user.EmailConfirmed = false;
            user.SessionVersion++;
        }

        if (mobile != user.PhoneNumber)
        {
            user.PhoneNumber = mobile;
            user.PhoneNumberConfirmed = false;
            user.SessionVersion++;
        }

        Check(await users.UpdateAsync(user));
        return true;
    }

    private async Task<bool> SetActiveCoreAsync(
        Guid actor, Guid id, ActiveUserInput input, CancellationToken ct)
    {
        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var user = await User(id, ct);

        if (user.ConcurrencyStamp != input.ConcurrencyStamp)
            throw new CompletionException(409, "اطلاعات کاربر تغییر کرده است.");

        if (!input.IsActive)
        {
            if (actor == id)
                throw new CompletionException(
                    409, "غیرفعال‌سازی حساب جاری مجاز نیست.");

            if (user.IsPlatformSuperAdmin &&
                !await db.Users.AnyAsync(
                    x => x.Id != id && x.IsActive &&
                         x.IsPlatformSuperAdmin, ct))
            {
                throw new CompletionException(
                    409, "آخرین ادمین فعال قابل غیرفعال‌سازی نیست.");
            }
        }

        user.IsActive = input.IsActive;
        user.SessionVersion++;

        Check(await users.UpdateAsync(user));
        await tx.CommitAsync(ct);
        return true;
    }

    private async Task<bool> ResetPasswordCoreAsync(
        Guid actor, Guid id, string password, CancellationToken ct)
    {
        if (actor == id)
            throw new CompletionException(
                400, "برای حساب خودتان از تغییر گذرواژه استفاده کنید.");

        if (string.IsNullOrEmpty(password) || password.Length > 128)
            throw new CompletionException(400, "طول گذرواژه معتبر نیست.");

        var user = await User(id, ct);
        user.MustChangePassword = true;
        user.SessionVersion++;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        Check(await users.ResetPasswordAsync(user, token, password));
        return true;
    }



    public Task<Guid> CreateManagedAsync(
       CreateUserInput input,
       CancellationToken cancellationToken)
       => UserWriteConflict.Run(
           () => CreateUserAsync(input, true, cancellationToken));

    public Task<Guid> RegisterAsync(
        CreateUserInput input,
        CancellationToken cancellationToken)
        => UserWriteConflict.Run(
            () => CreateUserAsync(input, false, cancellationToken));

    public Task<bool> UpdateUserAsync(
        Guid actorId,
        Guid userId,
        UpdateUserInput input,
        CancellationToken cancellationToken)
        => UserWriteConflict.Run(
            () => UpdateCoreAsync(
                actorId, userId, input, cancellationToken));

    public Task<bool> SetActiveAsync(
        Guid actorId,
        Guid userId,
        ActiveUserInput input,
        CancellationToken cancellationToken)
        => UserWriteConflict.Run(
            () => SetActiveCoreAsync(
                actorId, userId, input, cancellationToken));

    public Task<bool> ResetPasswordAsync(
        Guid actorId,
        Guid userId,
        string password,
        CancellationToken cancellationToken)
        => UserWriteConflict.Run(
            () => ResetPasswordCoreAsync(
                actorId, userId, password, cancellationToken));



}