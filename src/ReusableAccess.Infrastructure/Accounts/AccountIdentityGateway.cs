using Microsoft.AspNetCore.Identity;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.Accounts;
public sealed class AccountIdentityGateway(ApplicationDbContext db, UserManager<ApplicationUser> users) : IAccountIdentityGateway
{
    private ApplicationUser Tracked(AccountRecord account)
        => db.Users.Local.SingleOrDefault(u => u.Id == account.Id)
            ?? throw new InvalidOperationException("Load the account in this scope before using Identity.");
    private static AccountIdentityResult Result(IdentityResult result)
        => new(result.Succeeded, result.Errors.Select(e => new AccountIdentityError(e.Code, e.Description)).ToArray());
    private async Task<AccountIdentityResult> Write(AccountRecord account, Func<ApplicationUser, Task<IdentityResult>> action)
    {
        var u = Tracked(account);
        AccountIdentityMapping.Apply(account, u);
        var result = await action(u);
        account.ConcurrencyStamp = u.ConcurrencyStamp;
        return Result(result);
    }
    public async Task<AccountIdentityResult> CreateAsync(AccountRecord account, string password)
    {
        var u = new ApplicationUser();
        AccountIdentityMapping.Apply(account, u);
        var result = await users.CreateAsync(u, password);
        account.ConcurrencyStamp = u.ConcurrencyStamp;
        return Result(result);
    }
    public Task<AccountIdentityResult> UpdateAsync(AccountRecord account) => Write(account, users.UpdateAsync);
    public Task<AccountIdentityResult> ChangePasswordAsync(AccountRecord account, string currentPassword, string newPassword)
        => Write(account, u => users.ChangePasswordAsync(u, currentPassword, newPassword));
    public Task<string> GeneratePasswordResetTokenAsync(AccountRecord account)
    {
        var u = Tracked(account); AccountIdentityMapping.Apply(account, u);
        return users.GeneratePasswordResetTokenAsync(u);
    }
    public Task<AccountIdentityResult> ResetPasswordAsync(AccountRecord account, string token, string password)
        => Write(account, u => users.ResetPasswordAsync(u, token, password));
    public Task<AccountIdentityResult> AccessFailedAsync(AccountRecord account) => Write(account, users.AccessFailedAsync);
    public Task<AccountIdentityResult> ResetAccessFailedCountAsync(AccountRecord account) => Write(account, users.ResetAccessFailedCountAsync);
    public Task<AccountIdentityResult> SetLockoutEndDateAsync(AccountRecord account, DateTimeOffset? end)
        => Write(account, u => users.SetLockoutEndDateAsync(u, end));
    public Task<bool> IsLockedOutAsync(AccountRecord account) => users.IsLockedOutAsync(Tracked(account));
}
