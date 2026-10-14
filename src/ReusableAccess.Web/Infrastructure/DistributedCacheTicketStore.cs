using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;

namespace ReusableAccess.Web.Infrastructure;

public sealed class DistributedCacheTicketStore(
    IDistributedCache cache) : ITicketStore
{
    private const string KeyPrefix = "ReusableAccess:AuthTicket:";

    public async Task<string> StoreAsync(
        AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        await SaveAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(
        string key,
        AuthenticationTicket ticket) =>
        SaveAsync(key, ticket);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await cache.GetAsync(KeyPrefix + key);
        return bytes is null
            ? null
            : TicketSerializer.Default.Deserialize(bytes);
    }

    public Task RemoveAsync(string key) =>
        cache.RemoveAsync(KeyPrefix + key);

    private Task SaveAsync(string key, AuthenticationTicket ticket)
    {
        var expires = ticket.Properties.ExpiresUtc
            ?? DateTimeOffset.UtcNow.AddHours(8);

        return cache.SetAsync(
            KeyPrefix + key,
            TicketSerializer.Default.Serialize(ticket),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expires
            });
    }
}