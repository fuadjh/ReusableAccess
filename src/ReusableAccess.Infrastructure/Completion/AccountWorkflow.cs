using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Identity;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;
using ReusableAccess.Application.Users;

namespace ReusableAccess.Infrastructure.Completion;

public sealed class AccountWorkflow(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IUserRegistrationStore registrations,
    IAccountMessageSender sender,
    IRecoveryCodeQueue recoveryQueue,
    IDataProtectionProvider protection,
    IHttpContextAccessor http,
    TimeProvider time,
    ILogger<AccountWorkflow> logger) : IAccountWorkflow
{
    private readonly IDataProtector _codes =
        protection.CreateProtector("ReusableAccess.AccountCodes.v1");

    private const string Invalid =
        "اطلاعات ورود یا کد تأیید معتبر نیست؛ دوباره تلاش کنید.";

    private Task<AuthenticationPolicy> Policy(CancellationToken ct)
        => db.Set<AuthenticationPolicy>().AsNoTracking()
            .SingleAsync(x => x.Id == 1, ct);

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new CompletionException(
                400, string.Join(" ", result.Errors.Select(x => x.Description)));
    }

    public async Task<PublicAuthOptions> OptionsAsync(CancellationToken ct)
    {
        var p = await Policy(ct);
        return new(
            p.IsRegistrationOpen(time.GetUtcNow()),
            p.RecoveryEnabled,
            p.LoginMode);
    }

    public async Task<LoginOutcome> PasswordAsync(
        string nationalId, string password,
        bool remember, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 128)
            return new(false, Error: Invalid);

        var national = IdentityRules.NormalizeDigits(nationalId);

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.NationalId == national, ct);

        if (user is null || !user.IsActive ||
            !IdentityRules.IsValidNationalId(national))
            return new(false, Error: Invalid);

        var result = await signIn.CheckPasswordSignInAsync(
            user, password, lockoutOnFailure: true);

        if (!result.Succeeded)
            return new(false, Error: Invalid);

        var policy = await Policy(ct);

        if (policy.LoginMode == LoginMode.Password)
            return await CompleteLogin(user, remember, ct);

        var channel = policy.LoginMode == LoginMode.PasswordAndEmail
            ? DeliveryChannel.Email : DeliveryChannel.Sms;

        return new(false, await Issue(
            user, "login", channel, remember, true, ct));
    }

    private async Task<LoginOutcome> CompleteLogin(
        ApplicationUser user, bool remember, CancellationToken ct)
    {
        if (user.MustChangePassword)
        {
            var id = await Issue(
                user, "password-change",
                DeliveryChannel.Email, remember, false, ct);

            return new(false, id, PasswordChangeRequired: true);
        }

        await signIn.SignInWithClaimsAsync(
            user, remember,
            [
                new Claim(
                    "ra_version",
                    user.SessionVersion.ToString(CultureInfo.InvariantCulture))
            ]);

        return new(true);
    }

    private async Task<Guid> Issue(
        ApplicationUser user,
        string purpose,
        DeliveryChannel channel,
        bool remember,
        bool deliver,
        CancellationToken ct,
        bool deferred = false)
    {
        var now = time.GetUtcNow();

        if (deliver && await db.Set<AccountChallenge>().AnyAsync(
                x => x.UserId == user.Id &&
                     x.Purpose == purpose &&
                     x.CreatedUtc > now.AddMinutes(-1), ct))
        {
            throw new CompletionException(
                429, "برای ارسال مجدد یک دقیقه صبر کنید.");
        }

        var challenge = new AccountChallenge
        {
            UserId = user.Id,
            Purpose = purpose,
            Channel = channel,
            RememberMe = remember,
            UserSessionVersion = user.SessionVersion,
            CreatedUtc = now,
            ExpiresUtc = now.AddMinutes(5)
        };

        var code = RandomNumberGenerator.GetInt32(100000, 1000000)
            .ToString(CultureInfo.InvariantCulture);

        challenge.ProtectedCode = deliver
            ? _codes.Protect($"{challenge.Id:N}:{code}")
            : "";

        db.Add(challenge);
        await db.SaveChangesAsync(ct);

        if (deliver)
        {
            var destination = channel == DeliveryChannel.Email
                ? user.Email : user.PhoneNumber;

            if (string.IsNullOrWhiteSpace(destination))
                throw new CompletionException(
                    400, "اطلاعات کانال حساب کامل نیست.");

            try
            {
                if (deferred)
                {
                    if (!recoveryQueue.TryEnqueue(channel, destination, code))
                        throw new CompletionException(503, "صف ارسال پر است.");
                }
                else
                {
                    await sender.SendAsync(channel, destination, code, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                challenge.Consumed = true;
                challenge.Version++;
                await db.SaveChangesAsync(ct);

                logger.LogWarning(
                    "Account delivery failed for channel {Channel}.", channel);

                throw new CompletionException(
                    503, "ارسال کد انجام نشد؛ بعداً تلاش کنید.");
            }
        }

        return challenge.Id;
    }

    private async Task<(ApplicationUser User, AccountChallenge Challenge)> Consume(
        Guid id,
        string purpose,
        string? code,
        Guid? expectedUser,
        Func<ApplicationUser, AccountChallenge, Task>? effect,
        CancellationToken ct)
    {
        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var c = await db.Set<AccountChallenge>()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (c is null || c.Purpose != purpose ||
            c.Consumed || c.Attempts >= 5 ||
            c.ExpiresUtc <= time.GetUtcNow() ||
            (expectedUser.HasValue && c.UserId != expectedUser))
        {
            throw new CompletionException(400, Invalid);
        }

        var user = await db.Users
            .SingleOrDefaultAsync(x => x.Id == c.UserId, ct);

        if (user is null || !user.IsActive ||
            user.SessionVersion != c.UserSessionVersion)
            throw new CompletionException(400, Invalid);

        if (purpose == "login" && await users.IsLockedOutAsync(user))
            throw new CompletionException(400, Invalid);

        // در password-change، خود شناسهٔ تصادفی چالش پس از اثبات رمز صادر شده است.
        var valid = purpose == "password-change";

        if (!valid)
        {
            var normalized = IdentityRules.NormalizeDigits(code);

            try
            {
                valid =
                    normalized.Length == 6 &&
                    normalized.All(char.IsAsciiDigit) &&
                    CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(_codes.Unprotect(c.ProtectedCode)),
                        Encoding.UTF8.GetBytes($"{c.Id:N}:{normalized}"));
            }
            catch (CryptographicException)
            {
                valid = false;
            }
        }

        if (!valid)
        {
            c.Attempts++;
            c.Version++;
            await db.SaveChangesAsync(ct);

            if (purpose == "login")
                Check(await users.AccessFailedAsync(user));

            await tx.CommitAsync(ct);
            throw new CompletionException(400, Invalid);
        }

        if (effect is not null)
            await effect(user, c);

        c.Consumed = true;
        c.Version++;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (user, c);
    }

    public async Task<LoginOutcome> VerifyLoginAsync(
        Guid id, string code, CancellationToken ct)
    {
        var policy = await Policy(ct);

        var result = await Consume(
            id, "login", code, null,
            async (user, challenge) =>
            {
                var expected = policy.LoginMode == LoginMode.PasswordAndEmail
                    ? DeliveryChannel.Email : DeliveryChannel.Sms;

                if (policy.LoginMode == LoginMode.Password ||
                    challenge.Channel != expected)
                    throw new CompletionException(400, Invalid);

                if (challenge.Channel == DeliveryChannel.Email)
                    user.EmailConfirmed = true;
                else
                    user.PhoneNumberConfirmed = true;

                Check(await users.ResetAccessFailedCountAsync(user));
                Check(await users.UpdateAsync(user));
            }, ct);

        return await CompleteLogin(
            result.User, result.Challenge.RememberMe, ct);
    }

    public async Task<LoginOutcome> ChangeRequiredPasswordAsync(
        Guid id, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 128)
            throw new CompletionException(400, "طول گذرواژه معتبر نیست.");

        var result = await Consume(
            id, "password-change", null, null,
            async (user, _) =>
            {
                user.SessionVersion++;
                user.MustChangePassword = false;

                var token = await users.GeneratePasswordResetTokenAsync(user);
                Check(await users.ResetPasswordAsync(user, token, password));
            }, ct);

        return await CompleteLogin(
            result.User, result.Challenge.RememberMe, ct);
    }

    public async Task RegisterAsync(CreateUserInput input, CancellationToken ct)
    {
        if (!(await Policy(ct)).IsRegistrationOpen(time.GetUtcNow()))
            throw new CompletionException(
                403, "ثبت‌نام عمومی در این زمان فعال نیست.");

        try
        {
            await registrations.RegisterAsync(input, ct);
        }
        catch (CompletionException e) when (e.StatusCode == 409)
        {
            throw new CompletionException(
                400, "ثبت‌نام انجام نشد؛ اطلاعات را بررسی کنید.");
        }
    }

    public async Task<Guid> RequestRecoveryAsync(
        string nationalId, CancellationToken ct)
    {
        var policy = await Policy(ct);

        if (!policy.RecoveryEnabled)
            throw new CompletionException(403, "بازیابی گذرواژه غیرفعال است.");

        var national = IdentityRules.NormalizeDigits(nationalId);
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.NationalId == national, ct);

        // نتیجهٔ عمومی وجود حساب یا تأییدشدن کانال آن را افشا نمی‌کند.
        if (user is null || !user.IsActive ||
            (policy.RecoveryChannel == DeliveryChannel.Email
                ? !user.EmailConfirmed : !user.PhoneNumberConfirmed))
            return Guid.NewGuid();

        try
        {
            return await Issue(
                user, "recovery", policy.RecoveryChannel,
                false, true, ct, deferred: true);
        }
        catch (CompletionException)
        {
            return Guid.NewGuid();
        }
    }

    public async Task ResetRecoveredPasswordAsync(
        Guid id, string code, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 128)
            throw new CompletionException(400, "طول گذرواژه معتبر نیست.");

        var policy = await Policy(ct);

        if (!policy.RecoveryEnabled)
            throw new CompletionException(403, "بازیابی گذرواژه غیرفعال است.");

        await Consume(
            id, "recovery", code, null,
            async (user, challenge) =>
            {
                if (challenge.Channel != policy.RecoveryChannel)
                    throw new CompletionException(400, Invalid);

                user.SessionVersion++;
                user.MustChangePassword = false;

                var token = await users.GeneratePasswordResetTokenAsync(user);
                Check(await users.ResetPasswordAsync(user, token, password));
                Check(await users.SetLockoutEndDateAsync(user, null));
                Check(await users.ResetAccessFailedCountAsync(user));
            }, ct);
    }

    public async Task<Guid> RequestContactAsync(
        Guid userId, DeliveryChannel channel, CancellationToken ct)
    {
        if (!Enum.IsDefined(channel))
            throw new CompletionException(400, "کانال معتبر نیست.");

        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId && x.IsActive, ct)
            ?? throw new CompletionException(404, "حساب پیدا نشد.");

        return await Issue(
            user, "contact", channel, false, true, ct);
    }

    public async Task ConfirmContactAsync(
        Guid userId, Guid id, string code, CancellationToken ct)
    {
        await Consume(
            id, "contact", code, userId,
            async (user, challenge) =>
            {
                if (challenge.Channel == DeliveryChannel.Email)
                    user.EmailConfirmed = true;
                else
                    user.PhoneNumberConfirmed = true;

                Check(await users.UpdateAsync(user));
            }, ct);
    }

    public Task SignOutAsync()
        => http.HttpContext!.SignOutAsync(
            IdentityConstants.ApplicationScheme);
}