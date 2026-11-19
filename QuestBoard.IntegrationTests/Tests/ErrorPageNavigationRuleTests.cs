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
    [Fact]
    public async Task ErrorEndpoint_VisitedByAScript_AnswersTheBareStatusWithNoBody()
    {
        var (status, body) = await ErrorPageRenderTests.SendAsync(factory.CreateClient(), Request("/Error/404", "*/*", "empty"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task HealthEndpoint_ForABrowserAcceptingHtml_KeepsItsPlainTextBody()
    {
        var response = await factory.CreateClient().SendAsync(
            ErrorPageRenderTests.Navigation("/health"), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        body.Should().NotContain("<h1");
    }

    [Fact]
    public async Task JobDashboardPath_ForANavigation_IsNeverGivenTheErrorPage()
    {
        var client = await SignedInClientAsync("hangfire");

        var (_, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/hangfire/missing"));

        body.Should().NotContain("Page not found");
    }

    [Theory]
    [InlineData("/css/does-not-exist.css", "text/css,*/*;q=0.1", "style")]
    [InlineData("/js/does-not-exist.js", "*/*", "script")]
    [InlineData("/images/does-not-exist.png", "image/avif,image/webp,*/*", "image")]
    [InlineData("/css/does-not-exist.css", "text/html,*/*;q=0.8", "style")]
    public async Task MissingStaticFile_RequestedAsAnAsset_StaysABareNotFound(string url, string accept, string fetchDest)
    {
        var (status, body) = await ErrorPageRenderTests.SendAsync(factory.CreateClient(), Request(url, accept, fetchDest));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRoute_RequestedWithHead_AnswersTheStatusAndNoBody()
    {
        var client = await SignedInClientAsync("headnav");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, ErrorPageRenderTests.Navigation("/no-such-route-here", HttpMethod.Head));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOnlyRecordPage_RequestedWithHead_AnswersItsStatusAndNoBody()
    {
        var client = await SignedInClientAsync("headrecord");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, ErrorPageRenderTests.Navigation($"/Quest/Details/{int.MaxValue}", HttpMethod.Head));

        ((int)status).Should().BeInRange(400, 599);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRoute_PostedByANavigation_KeepsItsStatusAndGetsThePage()
    {
        var client = await SignedInClientAsync("postnav");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, ErrorPageRenderTests.Navigation("/no-such-route-here", HttpMethod.Post));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("Page not found");
        body.Should().Contain("Error 404");
    }

    [Theory]
    [InlineData("/no-such-route-here")]
    [InlineData("/Error/500")]
    [InlineData("/Error/403")]
    public async Task EveryThemedPage_IsAnsweredWithNoStoreCaching(string url)
    {
        var client = await SignedInClientAsync("nostore");

        var response = await client.SendAsync(
            ErrorPageRenderTests.Navigation(url), TestContext.Current.CancellationToken);

        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Pragma.ToString().Should().Contain("no-cache");
    }
}
