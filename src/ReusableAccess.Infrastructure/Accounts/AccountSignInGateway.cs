using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.Accounts;
public sealed class AccountSignInGateway(ApplicationDbContext db, SignInManager<ApplicationUser> signIn, IHttpContextAccessor http)
    : IAccountSignInGateway
{
    private ApplicationUser Tracked(AccountRecord account)
        => db.Users.Local.SingleOrDefault(u => u.Id == account.Id) ?? throw new InvalidOperationException("Account not loaded in this scope.");
    public async Task<AccountPasswordCheck> CheckPasswordSignInAsync(AccountRecord account, string password, bool lockoutOnFailure)
        => new((await signIn.CheckPasswordSignInAsync(Tracked(account), password, lockoutOnFailure)).Succeeded);
    public Task SignInAsync(AccountRecord account, bool remember)
        => signIn.SignInWithClaimsAsync(Tracked(account), remember,
            [new Claim("ra_version", account.SessionVersion.ToString(CultureInfo.InvariantCulture))]);
    public Task SignOutAsync()
        => (http.HttpContext ?? throw new InvalidOperationException("An HTTP request is required."))
            .SignOutAsync(IdentityConstants.ApplicationScheme);
}
