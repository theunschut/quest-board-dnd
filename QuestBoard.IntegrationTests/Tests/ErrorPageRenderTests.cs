using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;

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
    public async Task MissingRecord_ForASignedInViewer_OffersALogOutFormAndNoLogInLink()
    {
        var client = await SignedInClientAsync("nflogout");

        var (_, body) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));

        var form = Regex.Match(body, "<form\\b[^>]*action=\"/Account/Logout\"[^>]*>(?<inner>.*?)</form>", RegexOptions.Singleline);
        form.Success.Should().BeTrue();
        form.Value.Should().Contain("method=\"post\"");
        form.Groups["inner"].Value.Should().Contain("name=\"__RequestVerificationToken\"");
        form.Groups["inner"].Value.Should().MatchRegex("name=\"toLogin\"[^>]*value=\"true\"|value=\"true\"[^>]*name=\"toLogin\"");
        form.Groups["inner"].Value.Should().Contain("btn btn-secondary");
        body.Should().NotContain("/Account/Login");
    }

    [Fact]
    public async Task MissingRecord_ForAnAnonymousViewer_OffersALogInLinkAndNoLogOutForm()
    {
        var (_, body) = await SendAsync(factory.CreateClient(), Navigation("/Error/404"));

        body.Should().Contain("href=\"/Account/Login\"");
        body.Should().NotContain("/Account/Logout");
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
        await ErrorPageComparison.PrimeAsync(client);

        var (firstStatus, first) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));
        var (secondStatus, second) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue - 7}"));

        firstStatus.Should().Be(HttpStatusCode.NotFound);
        secondStatus.Should().Be(HttpStatusCode.NotFound);
        ErrorPageComparison.AssertSameBody(first, second);
    }

    [Fact]
    public async Task ErrorEndpoint_VisitedDirectlyAsANavigation_AnswersTheSamePage()
    {
        var client = await SignedInClientAsync("nfdirect");
        await ErrorPageComparison.PrimeAsync(client);

        var (viaMissing, missingBody) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));
        var (status, body) = await SendAsync(client, Navigation("/Error/404"));

        status.Should().Be(HttpStatusCode.NotFound);
        viaMissing.Should().Be(HttpStatusCode.NotFound);
        ErrorPageComparison.AssertSameBody(body, missingBody);
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

    private static string? AnchorTag(string body, string marker)
    {
        var match = Regex.Match(body, "<a\\b[^>]*" + Regex.Escape(marker) + "[^>]*>");
        return match.Success ? match.Value : null;
    }

    private static HttpRequestMessage NavigationFrom(string url, string? referer, HttpMethod? method = null)
    {
        var request = Navigation(url, method);
        if (referer != null)
            request.Headers.TryAddWithoutValidation("Referer", referer);
        return request;
    }

    [Fact]
    public async Task GoBack_WithASameSiteReferer_LinksToThatLocalPage()
    {
        var client = await SignedInClientAsync("backlocal");

        var (_, body) = await SendAsync(client, NavigationFrom($"/Quest/Details/{int.MaxValue}", "http://localhost/Quest/Details/5?x=1"));

        var anchor = AnchorTag(body, "data-history-back");
        anchor.Should().NotBeNull();
        anchor.Should().Contain("href=\"/Quest/Details/5?x=1\"");
        anchor.Should().NotMatchRegex("\\shidden\\b");
        anchor.Should().Contain("btn btn-secondary");
    }

    [Theory]
    [InlineData("https://evil.example/Quest")]
    [InlineData("//evil.example/Quest")]
    [InlineData("not a url")]
    [InlineData("http://localhost/Error/404")]
    [InlineData(null)]
    public async Task GoBack_WithNoUsableReferer_IsHiddenAndHasNoHref(string? referer)
    {
        var client = await SignedInClientAsync("backhidden");

        var (_, body) = await SendAsync(client, NavigationFrom($"/Quest/Details/{int.MaxValue}", referer));

        var anchor = AnchorTag(body, "data-history-back");
        anchor.Should().NotBeNull();
        anchor.Should().MatchRegex("\\shidden\\b");
        anchor.Should().NotContain("href=");
        body.Should().NotContain("evil.example");
    }

    [Fact]
    public async Task ActionRow_OffersEachActionOnceInTheDocumentedOrderWithOnePrimary()
    {
        var client = await SignedInClientAsync("actionorder");

        var (_, body) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));

        Regex.Matches(body, "btn-primary").Count.Should().Be(1);
        var back = body.IndexOf("data-history-back", StringComparison.Ordinal);
        var logout = body.IndexOf("/Account/Logout", StringComparison.Ordinal);
        var home = body.IndexOf("href=\"/\"", StringComparison.Ordinal);
        back.Should().BeGreaterThan(-1);
        logout.Should().BeGreaterThan(back);
        home.Should().BeGreaterThan(logout);
        Regex.Matches(body, "<i class=\"fas [^\"]* me-2\" aria-hidden=\"true\"></i>").Count.Should().BeGreaterThanOrEqualTo(3);
    }

    private async Task<HttpClient> SignedInClientWithBoardsAsync(string prefix, int boards)
    {
        await TestDataHelper.SeedDefaultGroupAsync(factory.Services);
        var client = await SignedInClientAsync(prefix);
        if (boards < 2)
            return client;

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var userId = int.Parse(
            client.DefaultRequestHeaders.Authorization!.Parameter!.Split(':')[0]);
        var second = new GroupEntity { Name = "Second_" + Guid.NewGuid().ToString("N")[..8], CreatedAt = DateTime.UtcNow };
        context.Groups.Add(second);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = second.Id, GroupRole = (int)GroupRole.Player });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return client;
    }

    [Fact]
    public async Task SwitchBoard_OnANotFound_ForAViewerOfTwoBoards_LinksToThePicker()
    {
        var client = await SignedInClientWithBoardsAsync("switchtwo", boards: 2);

        var (_, body) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));

        var anchor = AnchorTag(body, "href=\"/groups/pick\"");
        anchor.Should().NotBeNull();
        anchor.Should().Contain("btn btn-info");
    }

    [Fact]
    public async Task SwitchBoard_ForAViewerOfOneBoard_IsNeverOffered()
    {
        var client = await SignedInClientWithBoardsAsync("switchone", boards: 1);

        var (_, body) = await SendAsync(client, Navigation($"/Quest/Details/{int.MaxValue}"));

        body.Should().NotContain("/groups/pick");
        body.Should().NotContain("btn-info");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(418)]
    public async Task SwitchBoard_OnAnythingButANotFound_IsNeverOffered(int code)
    {
        var client = await SignedInClientWithBoardsAsync("switchother", boards: 2);

        var (_, body) = await SendAsync(client, Navigation($"/Error/{code}"));

        body.Should().NotContain("/groups/pick");
    }

    [Fact]
    public async Task SwitchBoard_ForAnAnonymousViewer_IsNeverOffered()
    {
        var (_, body) = await SendAsync(factory.CreateClient(), Navigation("/Error/404"));

        body.Should().NotContain("/groups/pick");
    }

    [Fact]
    public async Task SwitchBoard_ReadsOnlyTheViewersMemberships_SoTwoMissingIdsStayIdentical()
    {
        var client = await SignedInClientWithBoardsAsync("switchparity", boards: 2);
        await ErrorPageComparison.PrimeAsync(client);

        var first = await client.SendAsync(Navigation($"/Quest/Details/{int.MaxValue}"), TestContext.Current.CancellationToken);
        var second = await client.SendAsync(Navigation($"/Quest/Details/{int.MaxValue - 3}"), TestContext.Current.CancellationToken);

        await ErrorPageComparison.AssertIndistinguishableAsync(first, second);
    }

    [Fact]
    public async Task LogIn_ForAnAnonymousViewerOnANotFound_CarriesNoReturnUrl()
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation("/no-such-page-here?a=1"));

        status.Should().Be(HttpStatusCode.NotFound);
        var anchor = AnchorTag(body, "/Account/Login");
        anchor.Should().NotBeNull();
        anchor.Should().NotContain("returnUrl");
        anchor.Should().NotContain("ReturnUrl");
        body.Should().NotContain("no-such-page-here");
    }

    [Fact]
    public async Task LogIn_ForAnAnonymousGetThatAnswersAnotherError_CarriesTheLocalPathAsReturnUrl()
    {
        var (status, body) = await SendAsync(factory.CreateClient(), Navigation("/Account/Logout?x=1"));

        status.Should().Be(HttpStatusCode.MethodNotAllowed);
        var anchor = AnchorTag(body, "/Account/Login");
        anchor.Should().NotBeNull();
        anchor.Should().Contain("returnUrl=%2FAccount%2FLogout%3Fx%3D1");
    }

    [Fact]
    public async Task LogIn_ForAnAnonymousPostThatAnswersAnotherError_NeverCarriesAReturnUrl()
    {
        var (status, body) = await SendAsync(
            factory.CreateClient(), Navigation("/Account/AccessDenied", HttpMethod.Post));

        status.Should().Be(HttpStatusCode.MethodNotAllowed);
        var anchor = AnchorTag(body, "/Account/Login");
        anchor.Should().NotBeNull();
        anchor.Should().NotContain("eturnUrl");
    }

    [Fact]
    public async Task LogIn_OnADirectVisitToTheErrorEndpoint_CarriesNoReturnUrl()
    {
        var (_, body) = await SendAsync(factory.CreateClient(), Navigation("/Error/405"));

        var anchor = AnchorTag(body, "/Account/Login");
        anchor.Should().NotBeNull();
        anchor.Should().NotContain("eturnUrl");
    }

    [Fact]
    public async Task LogIn_NeverAppearsForASignedInViewer()
    {
        var client = await SignedInClientAsync("noreturnauth");

        var (_, body) = await SendAsync(client, Navigation("/Error/405"));

        AnchorTag(body, "/Account/Login").Should().BeNull();
    }
}

/// <summary>
/// A factory whose board service answers everything except the membership read, which always fails: the
/// rest of the application still needs the service, and the error page must survive losing only this read.
/// </summary>
public class FailingMembershipReadFactory : WebApplicationFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            var original = services.Last(d => d.ServiceType == typeof(IGroupService));
            services.AddScoped<IGroupService>(provider =>
            {
                var inner = (IGroupService)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!);
                var proxy = DispatchProxy.Create<IGroupService, FailingMembershipProxy>();
                ((FailingMembershipProxy)(object)proxy).Inner = inner;
                return proxy;
            });
        });
    }

    public class FailingMembershipProxy : DispatchProxy
    {
        public IGroupService Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IGroupService.GetGroupsForUserAsync))
                throw new InvalidOperationException("the membership read is down");

            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }
    }
}

public class ErrorPageSwitchBoardFailureTests(FailingMembershipReadFactory factory) : IClassFixture<FailingMembershipReadFactory>
{
    [Fact]
    public async Task NotFound_WhenTheMembershipReadFails_StillRendersWithoutSwitchBoard()
    {
        await TestDataHelper.SeedDefaultGroupAsync(factory.Services);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"failgroup_{suffix}", $"failgroup_{suffix}@test.com");

        var (status, body) = await ErrorPageRenderTests.SendAsync(
            client, ErrorPageRenderTests.Navigation("/Error/404"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("Page not found");
        body.Should().NotContain("/groups/pick");
        body.Should().Contain("/Account/Logout");
    }
}
