using System.Net;
using System.Net.Http.Headers;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Nav-visibility tests for NAV-01..06 and D-04 (anonymous Calendar link).
/// Tests start RED — the layout gating does not exist until Plan 02 wires
/// GetBoardTypeAsync into _Layout.cshtml/_Layout.Mobile.cshtml.
/// </summary>
public class LayoutNavigationTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public LayoutNavigationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // -----------------------------------------------------------------------
    // NAV-01: Campaign+DM — Calendar link absent
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignDm_CalendarLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "nav01_dm", "nav01_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Calendar");
    }

    // -----------------------------------------------------------------------
    // NAV-02: Campaign+authenticated — Shop link absent
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignAuthenticated_ShopLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "nav02_player", "nav02_player@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("fa-store");
    }

    // -----------------------------------------------------------------------
    // NAV-03: Campaign+authenticated — Guild Members link PRESENT (regression guard)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignAuthenticated_GuildMembersLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "nav03_player", "nav03_player@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Guild Members");
    }

    // -----------------------------------------------------------------------
    // NAV-04: Campaign+DM — Manage Shop link absent
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignDm_ManageShopLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "nav04_dm", "nav04_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Manage Shop");
    }

    // -----------------------------------------------------------------------
    // NAV-05: Campaign+DM — Edit My Profile link absent
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignDm_EditMyProfileLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "nav05_dm", "nav05_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Edit My Profile");
    }

    // -----------------------------------------------------------------------
    // NAV-06: Campaign+authenticated — Players link absent
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignAuthenticated_PlayersLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "nav06_player", "nav06_player@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("fa-users me-");
    }

    // -----------------------------------------------------------------------
    // OneShot regression: all 5 allowlisted items remain present for OneShot+DM
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_OneShotDm_AllAllowlistedLinksPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "navos_dm", "navos_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Calendar");
        html.Should().Contain("fa-store");
        html.Should().Contain("Manage Shop");
        html.Should().Contain("Edit My Profile");
        html.Should().Contain("fa-users me-");
    }

    // -----------------------------------------------------------------------
    // D-04: anonymous visitor — Calendar link absent (both layouts)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_Anonymous_CalendarLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;

        var (response, html) = await GetWithUserAgentAsync("/", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Calendar");
    }
}
