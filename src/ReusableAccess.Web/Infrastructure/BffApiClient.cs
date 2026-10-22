using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;

namespace ReusableAccess.Web.Infrastructure;

public sealed class BffApiClient(IHttpClientFactory httpClientFactory)
{
    public async Task<HttpResponseMessage> GetAsync(
        string relativeUri,
        string accessToken,
        ActiveAccessProfile? activeProfile,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var client = httpClientFactory.CreateClient("Api");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            relativeUri);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        if (activeProfile is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "X-Active-Role",
                activeProfile.RoleCode);

            request.Headers.TryAddWithoutValidation(
                "X-Active-Scope-Mode",
                activeProfile.ScopeMode);

            if (!string.IsNullOrWhiteSpace(activeProfile.ScopeCode))
            {
                request.Headers.TryAddWithoutValidation(
                    "X-Active-Scope",
                    activeProfile.ScopeCode);
            }
        }

        return await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }
    public async Task<HttpResponseMessage> PostJsonAsync<T>(
    string relativeUri,
    string accessToken,
    T body,
    CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentNullException.ThrowIfNull(body);

        var client = httpClientFactory.CreateClient("Api");
        using var request = new HttpRequestMessage(HttpMethod.Post, relativeUri);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(body);

        return await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }
    public async Task<IResult> ForwardPutJsonAsync<T>(
    string relativeUri,
    string accessToken,
    T body,
    CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Api");
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                relativeUri);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(body);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);
            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return Results.Content(
                responseBody,
                contentType,
                statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "ارتباط با API برقرار نشد.");
        }
    }
    public async Task<IResult> ForwardPostJsonAsync<T>(
    string relativeUri,
    string accessToken,
    T body,
    CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Api");
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                relativeUri);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(body);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);
            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return Results.Content(
                responseBody,
                contentType,
                statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "ارتباط با API برقرار نشد.");
        }
    }
    public async Task<IResult> ForwardGetAsync(
        string relativeUri,
        string accessToken,
        ActiveAccessProfile? activeProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await GetAsync(
                relativeUri,
                accessToken,
                activeProfile,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return Results.Content(
                body,
                contentType,
                statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "ارتباط با API برقرار نشد.");
        }
    }
}