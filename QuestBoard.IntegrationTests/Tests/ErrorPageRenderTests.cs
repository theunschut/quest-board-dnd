using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The themed error page as a browser sees it: the right status, one heading, a docket line and the
/// fixed sentence for the status, and a 404 that never depends on what was asked for.
/// </summary>
public class ErrorPageRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string NotFoundSentence =
        "It may have been removed, or it may belong to a board you&#x27;re not viewing right now.";

    internal static HttpRequestMessage Navigation(string url, HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        return request;
    }

    internal static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<HttpClient> SignedInClientAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"{prefix}_{suffix}", $"{prefix}_{suffix}@test.com");
        return client;
    }

    [Fact]
    public async Task MissingRecord_AsANavigation_AnswersTheThemedNotFoundPage()
    {
        var client = await SignedInClientAsync("nfpage");

        var (status, body) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));

        status.Should().Be(HttpStatusCode.NotFound);
        Regex.Matches(body, "<h1\\b").Count.Should().Be(1);
        body.Should().Contain("Page not found");
        body.Should().Contain("Error 404");
        body.Should().Contain(NotFoundSentence);
        body.Should().MatchRegex("<title>\\s*Page not found - ");
    }

    [Fact]
    public async Task MissingRecord_NeverEchoesTheIdOrThePath()
    {
        var client = await SignedInClientAsync("nfecho");

        var (_, body) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));

        body.Should().NotContain(int.MaxValue.ToString());
        body.Should().NotContain("/Quest/Details");
    }

    [Fact]
    public async Task TwoMissingRecords_AnswerIdenticalBodies()
    {
        var client = await SignedInClientAsync("nftwo");

        var (firstStatus, first) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));
        var (secondStatus, second) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue - 7}"));

        firstStatus.Should().Be(HttpStatusCode.NotFound);
        secondStatus.Should().Be(HttpStatusCode.NotFound);
        second.Should().Be(first);
    }

    [Fact]
    public async Task ErrorEndpoint_VisitedDirectlyAsANavigation_AnswersTheSamePage()
    {
        var client = await SignedInClientAsync("nfdirect");

        var (viaMissing, missingBody) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));
        var (status, body) = await SendAsync(client, Navigation("/Error/404"));

        status.Should().Be(HttpStatusCode.NotFound);
        viaMissing.Should().Be(HttpStatusCode.NotFound);
        body.Should().Be(missingBody);
    }

    [Fact]
    public async Task ErrorPage_ReadsNothingFromTheBoard_AndLinksOnlyTheFoundationSheets()
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation("/Error/404"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("href=\"/\"");
        body.Should().NotContain("legacy-pages.css");
        body.Should().NotContain("toast");
    }
    public static TheoryData<int, string, string> CopyMap => new()
    {
        { 400, "That request could not be processed", "Something about that request was not valid. Go back and try again." },
        { 409, "Your board session has changed", "Pick your board again, then retry. Nothing was saved." },
        { 429, "Please wait a moment", "Too many requests. Please try again later." },
        { 500, "Something went wrong", "We hit a problem on our side. Please try again in a moment." },
        { 418, "Something went wrong", "That request could not be completed. Go back or return home." },
        { 503, "Something went wrong", "That request could not be completed. Go back or return home." },
    };

    [Theory]
    [MemberData(nameof(CopyMap))]
    public async Task ErrorEndpoint_AsANavigation_AnswersItsStatusWithTheCopyOfThatStatus(int code, string heading, string sentence)
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation($"/Error/{code}"));
        var text = WebUtility.HtmlDecode(body);

        ((int)status).Should().Be(code);
        Regex.Matches(body, "<h1\\b").Count.Should().Be(1);
        text.Should().Contain(heading);
        text.Should().Contain($"Error {code}");
        text.Should().Contain(sentence);
    }

    [Fact]
    public async Task ErrorEndpoint_Forbidden_SignedIn_AsksToContactAnAdministrator()
    {
        var client = await SignedInClientAsync("forbidauth");

        var (status, body) = await SendAsync(client, Navigation("/Error/403"));
        var text = WebUtility.HtmlDecode(body);

        status.Should().Be(HttpStatusCode.Forbidden);
        text.Should().Contain("You Don't Have Permission");
        text.Should().Contain("Error 403");
        text.Should().Contain("If you believe you should have access to this page, please contact an administrator.");
        text.Should().NotContain("Please log in to access this page.");
        text.Should().NotContain("Dungeon Master");
    }

    [Fact]
    public async Task ErrorEndpoint_Forbidden_SignedOut_AsksToLogIn()
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation("/Error/403"));
        var text = WebUtility.HtmlDecode(body);

        status.Should().Be(HttpStatusCode.Forbidden);
        text.Should().Contain("You Don't Have Permission");
        text.Should().Contain("Please log in to access this page.");
        text.Should().NotContain("please contact an administrator");
    }

    [Theory]
    [InlineData(200)]
    [InlineData(302)]
    [InlineData(999)]
    [InlineData(0)]
    public async Task ErrorEndpoint_WithACodeThatIsNotAnError_AnswersTheNotFoundPage(int code)
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation($"/Error/{code}"));

        status.Should().Be(HttpStatusCode.NotFound);
        WebUtility.HtmlDecode(body).Should().Contain("Page not found");
        body.Should().Contain("Error 404");
    }

    [Fact]
    public async Task ErrorEndpoint_WithNoCode_AnswersTheServerErrorCopy()
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation("/Error"));

        status.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().Contain("Error 500");
        WebUtility.HtmlDecode(body).Should().Contain("We hit a problem on our side. Please try again in a moment.");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ErrorEndpoint_AcceptsEveryVerbWithoutAnAntiforgeryToken(string verb)
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation("/Error/418", new HttpMethod(verb)));

        ((int)status).Should().Be(418);
        body.Should().Contain("Error 418");
    }
}
