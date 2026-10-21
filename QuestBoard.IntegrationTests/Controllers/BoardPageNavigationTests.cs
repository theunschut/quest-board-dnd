using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Asserts that both layouts, and My Agenda's Board Availability button, show only the pages the
/// active board offers, that the brand link leads to the board's home page and that the Quests
/// entry appears exactly when the home page is somewhere else. Every case runs on the desktop and
/// the mobile user agent because the two layouts are separate files that drift apart.
/// </summary>
public class BoardPageNavigationTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    // The Board Availability button on My Agenda is the only filled secondary button that carries
    // this label; the menu entry is a plain nav link.
    private static readonly Regex AgendaAvailabilityButton =
        new(@"<a[^>]*btn-secondary[^>]*>\s*<i[^>]*></i>\s*Board Availability", RegexOptions.Compiled);

    private static readonly Regex BrandTag =
        new(@"<a\b[^>]*class=""[^""]*navbar-brand[^""]*""[^>]*>", RegexOptions.Compiled);

    private static readonly Regex HrefAttribute = new(@"href=""([^""]*)""", RegexOptions.Compiled);

    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public BoardPageNavigationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // A fresh store with the default board per test, so seeding page settings always finds its board.
    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    // The board context and the stored settings are shared across the whole class fixture, so this
    // restores the baseline and no test's premise depends on which test ran before it.
    public async ValueTask DisposeAsync()
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        _factory.TestGroupContext.ActiveGroupId = 1;
        await BoardPageSeed.ResetAsync(_factory, 1);
    }

    private async Task<string> GetHtmlAsync(string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // Everything before the page body, so a link inside the page itself can never be mistaken for a
    // menu entry. Both layouts render their navigation ahead of the main element.
    private static string NavigationOf(string html)
    {
        var mainStart = html.IndexOf("<main", StringComparison.Ordinal);
        mainStart.Should().BeGreaterThan(0, "the layout renders a main element after its navigation");
        return html[..mainStart];
    }

    private static string BrandHref(string html)
    {
        var tag = BrandTag.Match(html);
        tag.Success.Should().BeTrue("the layout renders a brand link");
        var href = HrefAttribute.Match(tag.Value);
        href.Success.Should().BeTrue("the brand link has a destination");
        return href.Groups[1].Value;
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Unconfigured_OneShotPlayer_SeesEveryPageAndBrandLinksToQuestList(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "bpnav_unconfigured", "bpnav_unconfigured@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);
        var nav = NavigationOf(html);

        nav.Should().Contain("href=\"/Shop\"");
        nav.Should().Contain("href=\"/QuestLog\"");
        nav.Should().Contain("href=\"/Characters\"");
        nav.Should().Contain("href=\"/Contacts\"");
        nav.Should().Contain("href=\"/Players\"");
        nav.Should().Contain("href=\"/Calendar\"");
        nav.Should().NotContain("id=\"nav-quests");
        BrandHref(nav).Should().Be("/quests");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task SwitchedOffPagesWithCalendarHome_AreHiddenQuestsEntryAppearsAndBrandGoesToCalendar(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var disabled = BoardPageSet.Empty
            .With(BoardPage.Shop)
            .With(BoardPage.Contacts)
            .With(BoardPage.Characters);
        await BoardPageSeed.SetAsync(_factory, 1, disabled, BoardPage.Calendar);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "bpnav_calendarhome", "bpnav_calendarhome@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);
        var nav = NavigationOf(html);

        nav.Should().NotContain("href=\"/Shop\"");
        nav.Should().NotContain("href=\"/Contacts\"");
        nav.Should().NotContain("href=\"/Characters\"");
        nav.Should().Contain("href=\"/QuestLog\"");
        nav.Should().Contain("href=\"/Players\"");

        var questsId = userAgent == MobileUserAgent ? "id=\"nav-quests-mobile\"" : "id=\"nav-quests\"";
        nav.Should().Contain(questsId);
        var questsTag = Regex.Match(nav, @"<a\b[^>]*" + Regex.Escape(questsId) + @"[^>]*>");
        HrefAttribute.Match(questsTag.Value).Groups[1].Value.Should().Be("/quests");

        BrandHref(nav).Should().Be("/Calendar");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CalendarSwitchedOff_HidesTheCalendarMenuLink(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Calendar), BoardPage.Quests);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "bpnav_nocalendar", "bpnav_nocalendar@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);

        NavigationOf(html).Should().NotContain("href=\"/Calendar\"");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task DungeonMaster_WithEventsShopAndProfileOff_LosesThoseEntriesButKeepsCreateQuest(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var disabled = BoardPageSet.Empty
            .With(BoardPage.Events)
            .With(BoardPage.Shop)
            .With(BoardPage.DungeonMasterProfile);
        await BoardPageSeed.SetAsync(_factory, 1, disabled, BoardPage.Quests);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_dm_pagesoff", "bpnav_dm_pagesoff@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);
        var nav = NavigationOf(html);

        nav.Should().Contain("Create Quest");
        nav.Should().NotContain("Create Event");
        nav.Should().NotContain("Board Availability");
        nav.Should().NotContain("Manage Shop");
        nav.Should().NotContain("Edit My Profile");
    }

    [Fact]
    public async Task DungeonMasterDropdown_WhenEveryItemIsHidden_IsNotRendered()
    {
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var disabled = BoardPageSet.Empty.With(BoardPage.Events).With(BoardPage.Quests);
        await BoardPageSeed.SetAsync(_factory, 1, disabled, BoardPage.Calendar);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_dm_emptymenu", "bpnav_dm_emptymenu@test.com");

        var html = await GetHtmlAsync("/Agenda", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        html.Should().NotContain("id=\"dungeonMasterDropdown\"");
    }

    [Fact]
    public async Task DungeonMasterDropdown_WhenSomeItemsRemain_IsRendered()
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_dm_somemenu", "bpnav_dm_somemenu@test.com");

        var html = await GetHtmlAsync("/Agenda", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        html.Should().Contain("id=\"dungeonMasterDropdown\"");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Admin_WithQuestsOff_DoesNotSeeQuestManagement(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Calendar);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "bpnav_admin_noquests", "bpnav_admin_noquests@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);
        var nav = NavigationOf(html);

        nav.Should().Contain("User Management");
        nav.Should().NotContain("Quest Management");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Admin_WithQuestsOn_SeesQuestManagement(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "bpnav_admin_quests", "bpnav_admin_quests@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);

        NavigationOf(html).Should().Contain("Quest Management");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task DungeonMaster_WithNoResolvedBoardType_SeesTodaysMenu(string userAgent)
    {
        _factory.TestGroupContext.BoardType = null;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_dm_unresolved", "bpnav_dm_unresolved@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);
        var nav = NavigationOf(html);

        nav.Should().Contain("Create Quest");
        nav.Should().Contain("Create Event");
        nav.Should().Contain("href=\"/QuestLog\"");
        nav.Should().Contain("href=\"/Characters\"");
        nav.Should().Contain("href=\"/Contacts\"");
        nav.Should().NotContain("Board Availability");
        nav.Should().NotContain("href=\"/Shop\"");
        nav.Should().NotContain("href=\"/Players\"");
        nav.Should().NotContain("href=\"/Calendar\"");
        nav.Should().NotContain("id=\"nav-quests");
        BrandHref(nav).Should().Be("/quests");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Agenda_WithEventsOff_HidesBoardAvailabilityButton(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_agenda_eventsoff", "bpnav_agenda_eventsoff@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);

        html.Should().NotContain("Board Availability");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Agenda_WithEventsOn_ShowsBoardAvailabilityButton(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_agenda_eventson", "bpnav_agenda_eventson@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);

        AgendaAvailabilityButton.IsMatch(html).Should().BeTrue();
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Agenda_WithNoResolvedBoardType_StillShowsBoardAvailabilityButtonToDungeonMaster(string userAgent)
    {
        _factory.TestGroupContext.BoardType = null;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, "bpnav_agenda_unresolved", "bpnav_agenda_unresolved@test.com");

        var html = await GetHtmlAsync("/Agenda", userAgent, authClient.DefaultRequestHeaders.Authorization);

        AgendaAvailabilityButton.IsMatch(html).Should().BeTrue();
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Anonymous_BrandLinksToHome(string userAgent)
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty, BoardPage.Calendar);

        var html = await GetHtmlAsync("/", userAgent, null);

        BrandHref(html).Should().Be("/");
    }
}
