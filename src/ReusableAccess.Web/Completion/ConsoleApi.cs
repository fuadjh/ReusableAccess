using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using ReusableAccess.Web.Infrastructure;
using ReusableAccess.Web.Ui;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace ReusableAccess.Web.Completion;

public sealed class ConsoleApiException(int status, string message)
    : Exception(message)
{
    public int StatusCode { get; } = status;
}

public sealed class ConsoleApi(
    IHttpClientFactory clients,
    ITicketStore tickets,
    AuthenticationStateProvider auth,
    ActiveAccessProfileStore profiles)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    public async Task<bool> AllowedAsync(
        string permission, ClaimsPrincipal? principal = null)
    {
        try
        {
            using var response = await Send(
                HttpMethod.Get,
                "api/console/check/" + Uri.EscapeDataString(permission),
                null, true, principal);

            return response.IsSuccessStatusCode;
        }
        catch (ConsoleApiException e) when (e.StatusCode is 401 or 403)
        {
            return false;
        }
    }

    private async Task<HttpResponseMessage> Send(
        HttpMethod method,
        string uri,
        object? body,
        bool withProfile,
        ClaimsPrincipal? principal = null)
    {
        principal ??= (await auth.GetAuthenticationStateAsync()).User;

        var key = principal.FindFirstValue(
            DistributedCacheTicketStore.SessionClaim);

        if (string.IsNullOrEmpty(key))
            throw new ConsoleApiException(
                401, "نشست معتبر نیست؛ دوباره وارد شوید.");

        var ticket = await tickets.RetrieveAsync(key);
        var subject = principal.FindFirstValue("sub");

        if (ticket is null ||
            ticket.Properties.ExpiresUtc <= DateTimeOffset.UtcNow ||
            string.IsNullOrWhiteSpace(subject) ||
            ticket.Principal.FindFirstValue("sub") != subject)
        {
            throw new ConsoleApiException(
                401, "نشست پایان یافته؛ دوباره وارد شوید.");
        }

        var token = ticket.Properties.GetTokenValue("access_token");

        if (string.IsNullOrWhiteSpace(token))
            throw new ConsoleApiException(401, "ورود دوباره لازم است.");

        if (uri.StartsWith('/') || uri.Contains("://") || uri.Contains(".."))
            throw new InvalidOperationException(
                "Only fixed relative API paths are accepted.");

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        if (withProfile &&
            profiles.TryGet(ticket.Properties, out var profile))
        {
            request.Headers.Add("X-Active-Role", profile.RoleCode);
            request.Headers.Add("X-Active-Scope-Mode", profile.ScopeMode);

            if (profile.ScopeCode is not null)
                request.Headers.Add("X-Active-Scope", profile.ScopeCode);
        }

        if (body is not null)
            request.Content = System.Net.Http.Json.JsonContent.Create(
                body, options: Json);

        HttpResponseMessage response;

        try
        {
            response = await clients.CreateClient("Api").SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new ConsoleApiException(502, "ارتباط با API برقرار نشد.");
        }
        catch (TaskCanceledException)
        {
            throw new ConsoleApiException(504, "مهلت پاسخ API پایان یافت.");
        }

        if (response.IsSuccessStatusCode)
            return response;

        var text = await response.Content.ReadAsStringAsync();
        var status = (int)response.StatusCode;
        response.Dispose();

        if (status == 401)
            await tickets.RemoveAsync(key);

        var message = status switch
        {
            401 => "نشست پایان یافته؛ دوباره وارد شوید.",
            403 => "۴۰۳ — مجوز انجام این عملیات را ندارید.",
            _ => $"درخواست انجام نشد ({status})."
        };

        message = ApiProblemText.Read(text, message);

        throw new ConsoleApiException(status, message);
    }

    public async Task<T> GetAsync<T>(string uri, bool profile = false)
    {
        using var response = await Send(
            HttpMethod.Get, uri, null, profile);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var data = json.RootElement.GetProperty("data")
            .Deserialize<T>(Json);

        return data is null
            ? throw new ConsoleApiException(502, "پاسخ API معتبر نیست.")
            : data;
    }

    public async Task<T> PostAsync<T>(string uri, object body)
    {
        using var response = await Send(
            HttpMethod.Post, uri, body, false);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var data = json.RootElement.GetProperty("data")
            .Deserialize<T>(Json);

        return data is null
            ? throw new ConsoleApiException(502, "پاسخ API معتبر نیست.")
            : data;
    }

    public async Task WriteAsync(
        HttpMethod method, string uri,
        object? body = null, bool profile = false)
    {
        using var response = await Send(method, uri, body, profile);
    }

    public async Task<string> RawAsync(string uri, bool profile = false)
    {
        using var response = await Send(
            HttpMethod.Get, uri, null, profile);

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<ActiveAccessProfile?> ActiveProfileAsync()
    {
        var principal = (await auth.GetAuthenticationStateAsync()).User;
        var key = principal.FindFirstValue(
            DistributedCacheTicketStore.SessionClaim);

        var ticket = key is null ? null : await tickets.RetrieveAsync(key);

        return profiles.TryGet(ticket?.Properties, out var profile)
            ? profile : null;
    }
}