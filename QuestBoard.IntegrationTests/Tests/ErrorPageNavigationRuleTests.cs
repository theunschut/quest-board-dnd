using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Only a browser asking for a page gets the HTML error page. A script's fetch, an image, a stylesheet,
/// a calendar client, the job dashboard and the health endpoint keep exactly the bare status they got
/// before the themed pages existed.
/// </summary>
public class ErrorPageNavigationRuleTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private async Task<HttpClient> SignedInClientAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"{prefix}_{suffix}", $"{prefix}_{suffix}@test.com");
        return client;
    }

    private static HttpRequestMessage Request(string url, string? accept, string? fetchDest = null, string? requestedWith = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (accept != null)
            request.Headers.TryAddWithoutValidation("Accept", accept);
        if (fetchDest != null)
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", fetchDest);
        if (requestedWith != null)
            request.Headers.TryAddWithoutValidation("X-Requested-With", requestedWith);
        return request;
    }

    [Fact]
    public async Task MissingRecord_FromAScriptFetch_StaysABareNotFound()
    {
        var client = await SignedInClientAsync("fetchbare");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, Request($"/Quest/Details/{int.MaxValue}", "*/*", "empty"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRecord_WithNoAcceptHeader_StaysABareNotFound()
    {
        var client = await SignedInClientAsync("noaccept");

        var (status, body) = await ErrorPageRenderTests.SendAsync(client, Request($"/Quest/Details/{int.MaxValue}", null));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRecord_FromAnXmlHttpRequestThatAcceptsHtml_StaysABareNotFound()
    {
        var client = await SignedInClientAsync("xhrbare");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, Request($"/Quest/Details/{int.MaxValue}", "text/html", requestedWith: "XMLHttpRequest"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRecord_FromAFetchThatAcceptsHtml_StaysABareNotFound()
    {
        var client = await SignedInClientAsync("fetchhtml");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, Request($"/Quest/Details/{int.MaxValue}", "text/html", "empty"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }
}
