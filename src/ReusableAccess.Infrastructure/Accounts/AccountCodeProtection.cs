using Microsoft.AspNetCore.DataProtection;
using ReusableAccess.Application.Accounts;
namespace ReusableAccess.Infrastructure.Accounts;
public sealed class AccountCodeProtection(IDataProtectionProvider provider) : IAccountCodeProtection
{
    // Purpose ثابت است تا تغییر Namespace کدهای صادرشده را باطل نکند.
    private readonly IDataProtector _protector = provider.CreateProtector("ReusableAccess.AccountCodes.v1");
    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}
