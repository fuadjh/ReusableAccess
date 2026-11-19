using System.Net;
using System.Text.Json;
using Xunit;

namespace ReusableAccess.Api.IntegrationTests;

public sealed partial class CompletionAuthorizationTests
{
    private const string WritableScopesUrl = "/api/console/writable-scopes";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Console_reads_preserve_data_and_scope(bool allScopes)
    {
        await using var host = await Host.Create(
            allowCreateAndDelete: true,
            useAllScopes: allScopes);

        host.As(host.Reader, mode: allScopes ? "all" : "selected");

        using var itemsResponse = await host.Client.GetAsync(ConsoleWorkItems);
        Assert.Equal(HttpStatusCode.OK, itemsResponse.StatusCode);

        using var itemsJson = JsonDocument.Parse(
            await itemsResponse.Content.ReadAsStringAsync());

        var root = itemsJson.RootElement;
        Assert.False(root.TryGetProperty("message", out _));

        var items = root.GetProperty("data").EnumerateArray().ToArray();

        // ترتیب فهرست بر اساس عنوان است: Other پیش از Own می‌آید.
        var expectedItemIds = allScopes
            ? new[] { host.OtherItem, host.OwnItem }
            : new[] { host.OwnItem };

        Assert.Equal(
            expectedItemIds,
            items.Select(item => item.GetProperty("id").GetGuid()).ToArray());

        Assert.All(items, item =>
        {
            var id = item.GetProperty("id").GetGuid();
            var expectedTitle = id == host.OwnItem
                ? "Own scope item"
                : "Other scope item";

            Assert.Equal(expectedTitle, item.GetProperty("title").GetString());
            Assert.Equal("Open", item.GetProperty("status").GetString());
        });

        using var scopesResponse = await host.Client.GetAsync(WritableScopesUrl);
        Assert.Equal(HttpStatusCode.OK, scopesResponse.StatusCode);

        using var scopesJson = JsonDocument.Parse(
            await scopesResponse.Content.ReadAsStringAsync());

        var scopes = scopesJson.RootElement.GetProperty("data")
            .EnumerateArray()
            .ToArray();

        var expectedScopeIds = allScopes
            ? new[] { host.ScopeAId, host.ScopeBId }
            : new[] { host.ScopeAId };

        Assert.Equal(
            expectedScopeIds,
            scopes.Select(scope => scope.GetProperty("scopeId").GetGuid()).ToArray());

        Assert.Equal(
            allScopes ? new[] { "A", "B" } : new[] { "A" },
            scopes.Select(scope => scope.GetProperty("scopeCode").GetString()).ToArray());
    }

    [Fact]
    public async Task Writable_scopes_require_create_permission()
    {
        await using var host = await Host.Create();
        host.As(host.Reader);

        using var response = await host.Client.GetAsync(WritableScopesUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Writable_scopes_do_not_accept_all_without_an_all_bundle()
    {
        await using var host = await Host.Create(allowCreateAndDelete: true);
        host.As(host.Reader, mode: "all");

        using var response = await host.Client.GetAsync(WritableScopesUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}