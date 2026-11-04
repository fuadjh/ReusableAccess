using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace ReusableAccess.Web.Infrastructure;

public sealed class DistributedCacheTicketStore(
    IDistributedCache cache,
    IDataProtectionProvider protection) : ITicketStore
{
    public const string SessionClaim = "ra_web_session";
    private const string Prefix = "ReusableAccess:AuthTicket:v2:";

    private readonly IDataProtector _protector =
        protection.CreateProtector("ReusableAccess.WebTickets.v2");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        await Save(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
        => Save(key, ticket);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var data = await cache.GetAsync(Prefix + key);

        if (data is null)
            return null;

        try
        {
            return TicketSerializer.Default.Deserialize(
                _protector.Unprotect(data));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            await RemoveAsync(key);
            return null;
        }
    }

    public Task RemoveAsync(string key)
        => cache.RemoveAsync(Prefix + key);

    private Task Save(string key, AuthenticationTicket ticket)
    {
        var identity = ticket.Principal.Identities
            .First(x => x.IsAuthenticated);

        foreach (var old in identity.FindAll(SessionClaim).ToArray())
            identity.RemoveClaim(old);

        identity.AddClaim(new Claim(SessionClaim, key));

        return cache.SetAsync(
            Prefix + key,
            _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration =
                    ticket.Properties.ExpiresUtc ??
                    DateTimeOffset.UtcNow.AddHours(8)
            });
    }
}