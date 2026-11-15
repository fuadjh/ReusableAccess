using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Application.Accounts;

// مدل سروری در مرز Identity؛ نه Entity مربوط به EF و نه ورودی قابل ارسال از مرورگر.
public sealed class AccountRecord
{
    public Guid Id { get; set; }
    public string NationalId { get; set; } = "";
    public string? UserName { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string OrganizationalCode { get; set; } = "";
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPlatformSuperAdmin { get; set; }
    public bool LockoutEnabled { get; set; }
    public bool MustChangePassword { get; set; }
    public int SessionVersion { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
public sealed record AccountIdentityError(string Code, string Description);
public sealed record AccountIdentityResult(bool Succeeded, IReadOnlyList<AccountIdentityError> Errors);
public sealed record AccountPasswordCheck(bool Succeeded);
public interface IAccountRepository
{
    Task<AuthenticationPolicy> ReadPolicyAsync(CancellationToken cancellationToken);
    Task<AuthenticationPolicy> ReadPolicyForUpdateAsync(CancellationToken cancellationToken);
    Task<AccountRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AccountRecord?> FindActiveByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AccountRecord?> FindByNationalIdAsync(string nationalId, CancellationToken cancellationToken);
    Task<bool> NationalIdExistsAsync(string nationalId, Guid? excludingUserId, CancellationToken cancellationToken);
    Task<bool> HasOtherActiveAdministratorAsync(Guid excludingUserId, CancellationToken cancellationToken);
    Task<bool> HasRecentChallengeAsync(Guid userId, string purpose, DateTimeOffset cutoff, CancellationToken cancellationToken);
    Task<AccountChallenge?> FindChallengeAsync(Guid id, CancellationToken cancellationToken);
    void AddChallenge(AccountChallenge challenge);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public interface IAccountIdentityGateway
{
    Task<AccountIdentityResult> CreateAsync(AccountRecord account, string password);
    Task<AccountIdentityResult> UpdateAsync(AccountRecord account);
    Task<AccountIdentityResult> ChangePasswordAsync(AccountRecord account, string currentPassword, string newPassword);
    Task<string> GeneratePasswordResetTokenAsync(AccountRecord account);
    Task<AccountIdentityResult> ResetPasswordAsync(AccountRecord account, string token, string password);
    Task<AccountIdentityResult> AccessFailedAsync(AccountRecord account);
    Task<AccountIdentityResult> ResetAccessFailedCountAsync(AccountRecord account);
    Task<AccountIdentityResult> SetLockoutEndDateAsync(AccountRecord account, DateTimeOffset? end);
    Task<bool> IsLockedOutAsync(AccountRecord account);
}
public interface IAccountSignInGateway
{
    Task<AccountPasswordCheck> CheckPasswordSignInAsync(AccountRecord account, string password, bool lockoutOnFailure);
    Task SignInAsync(AccountRecord account, bool remember);
    Task SignOutAsync();
}
public interface IAccountCodeProtection { string Protect(string value); string Unprotect(string value); }
// فقط ترجمهٔ خطای فنی؛ نه تراکنش و نه retry یا Unit of Work جدید.
public interface IUserWriteExecution { Task<T> RunAsync<T>(Func<Task<T>> action); }
