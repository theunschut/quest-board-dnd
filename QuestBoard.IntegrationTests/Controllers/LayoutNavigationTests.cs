using System.Net;
using System.Net.Http.Headers;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Asserts which navigation entries each role sees on each board type, on both the desktop and
/// the mobile layout. Anonymous visitors are covered too, so an entry that should only exist for
/// a signed-in board member cannot silently leak into the public navigation.
/// </summary>
public class LayoutNavigationTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
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

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // The board context is shared across the whole class fixture, so each test below sets the
    // board type it needs and this restores the baseline afterwards so no test's premise depends
    // on which test ran before it.
    public ValueTask DisposeAsync()
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        _factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
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
    // Campaign+DM — Calendar link present. The calendar carries the
    // recurring-session surfaces campaign boards depend on, so it stays in
    // the navigation on both board types rather than being hidden.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignDm_CalendarLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "navcal_dm", "navcal_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Calendar");
    }

    // -----------------------------------------------------------------------
    // Campaign+player — Calendar link present. The entry is gated on being
    // logged in, not on being a manager, so an ordinary player sees it too.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignPlayer_CalendarLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "navcal_player", "navcal_player@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Calendar");
    }

    // -----------------------------------------------------------------------
    // Campaign+anonymous — Calendar link absent. Regression guard for the
    // real risk in widening the board-type half of the condition: it must
    // not relax the logged-in half that sits alongside it.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignAnonymous_CalendarLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;

        var (response, html) = await GetWithUserAgentAsync("/", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Calendar");
    }

    // -----------------------------------------------------------------------
    // Campaign+authenticated — Shop link absent
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
    // Campaign+authenticated — Characters link PRESENT (regression guard)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignAuthenticated_CharactersLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "nav03_player", "nav03_player@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Characters");
    }

    // -----------------------------------------------------------------------
    // Campaign+DM — Manage Shop link absent
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
    // Campaign+DM — Edit My Profile link absent
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
    // Campaign+authenticated — Players link absent
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
    // Anonymous visitor — Calendar link absent (both layouts)
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

    // -----------------------------------------------------------------------
    // Create Event navbar entry — present for a Dungeon Master on both
    // layouts and both board types, absent for a Player
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_DungeonMaster_CreateEventEntryPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "navevent_dm", "navevent_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Create Event");
    }

    [Fact]
    public async Task Nav_Player_CreateEventEntryAbsent()
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "navevent_player", "navevent_player@test.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync("/quests", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Create Event");
        // Proves the navbar actually rendered rather than an error page the NotContain
        // assertion above would otherwise pass against for free.
        html.Should().Contain("Characters");
    }

    [Fact]
    public async Task Nav_CampaignBoard_DungeonMaster_CreateEventEntryStillPresent()
    {
        var previousBoardType = _factory.TestGroupContext.BoardType;
        try
        {
            // This entry sits alongside Create Quest, which is not board-type gated, so it
            // must remain available on every board type.
            _factory.TestGroupContext.BoardType = BoardType.Campaign;
            var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
                _factory, "navevent_campaign_dm", "navevent_campaign_dm@test.com");

            var (response, html) = await GetWithUserAgentAsync("/quests", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Create Event");
        }
        finally
        {
            _factory.TestGroupContext.BoardType = previousBoardType;
        }
    }

    // -----------------------------------------------------------------------
    // Availability Overview nav entry — these assert the new entry itself,
    // while the existing Calendar cases above assert the toggle label they
    // sit beside is unchanged. Present for DM and player on both board
    // types and both user agents, absent for an anonymous visitor.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignDm_AvailabilityOverviewLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "navavail_dm", "navavail_dm@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Availability Overview");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignPlayer_AvailabilityOverviewLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "navavail_player", "navavail_player@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Availability Overview");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_OneShotPlayer_AvailabilityOverviewLinkPresent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "navavail_oneshot", "navavail_oneshot@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Availability Overview");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Nav_CampaignAnonymous_AvailabilityOverviewLinkAbsent(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;

        var (response, html) = await GetWithUserAgentAsync("/", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Availability Overview");
    }
}
