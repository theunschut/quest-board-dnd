using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves a board can switch its quest list off, and what that means end to end: the switch is
/// refused while any quest is still live or the quest list is the home page, every way into the
/// quest pages lands on the board's home with an explanation, the menus and the board's own
/// calendar carry no quest, a linked board's quest entry turns to plain text, a Dungeon Master's
/// quest history stops linking, and switching the quest list back on restores everything as it was.
/// Menu, calendar and profile facts run on the desktop and the mobile user agent because the two
/// layouts are separate files that drift apart.
///
/// Seeded on board 1: a closed quest and a played quest, both in May 2020, plus an event in the
/// same month, so no quest is live unless a fact adds one.
///
/// What these facts do NOT establish: how the live-quest count translates to SQL. The shared
/// harness uses the EF Core InMemory provider; the count's boundary is pinned in the repository
/// unit tests.
/// </summary>
public class BoardPageQuestsOffTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const int BoardA = 1;
    private const int BoardB = 2;

    private const string QuestListToast = "The quest list isn't used on this board.";
    private const string QuestLogToast = "The Quest Log isn't used on this board.";
    private const string ClosedTitle = "Chronicle of Ashfall";
    private const string PlayedTitle = "Tavern Brawl at the Gilded Flagon";
    private const string EventTitle = "May Moot Gathering";
    private const string CalendarUrl = "/Calendar?year=2020&month=5";

    private static readonly DateTime ClosedDate = new(2020, 5, 10, 19, 0, 0);
    private static readonly DateTime PlayedDate = new(2020, 5, 17, 19, 0, 0);

    private static readonly int QuestsBit = BoardPageSet.Empty.With(BoardPage.Quests).Mask;
    private static readonly int QuestLogBit = BoardPageSet.Empty.With(BoardPage.QuestLog).Mask;

    private static readonly Regex BrandTag =
        new(@"<a\b[^>]*class=""[^""]*navbar-brand[^""]*""[^>]*>", RegexOptions.Compiled);

    private static readonly Regex HrefAttribute = new(@"href=""([^""]*)""", RegexOptions.Compiled);

    public static TheoryData<string> UserAgents => new() { DesktopUserAgent, MobileUserAgent };

    private readonly WebApplicationFactoryBase _factory;

    public BoardPageQuestsOffTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await BoardPageSeed.ResetAsync(_factory, BoardA);
        await BoardPageSeed.ResetAsync(_factory, BoardB);
        _factory.TestGroupContext.ActiveGroupId = BoardA;
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
    }

    private sealed record Scenario(UserEntity Dm, int ClosedQuestId, int PlayedQuestId);

    private async Task<Scenario> SeedAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.ActiveGroupId = BoardA;
        _factory.TestGroupContext.BoardType = BoardType.OneShot;

        var dm = await BoardMoveSeed.CreateUserAsync(_factory, "questsoff_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(_factory, dm.Id, BoardA, GroupRole.DungeonMaster);

        var closedId = await BoardMoveSeed.SeedQuestAsync(
            _factory, BoardA, ClosedTitle, dm.Id, [ClosedDate], finalizedDate: ClosedDate, isClosed: true);
        var playedId = await BoardMoveSeed.SeedQuestAsync(
            _factory, BoardA, PlayedTitle, dm.Id, [PlayedDate], finalizedDate: PlayedDate);
        await BoardMoveSeed.SeedEventAsync(_factory, BoardA, EventTitle, new DateOnly(2020, 5, 20));

        return new Scenario(dm, closedId, playedId);
    }

    private Task SwitchQuestsOffAsync(BoardPage home = BoardPage.Calendar) =>
        BoardPageSeed.SetAsync(_factory, BoardA, BoardPageSet.Empty.With(BoardPage.Quests), home);

    // -------------------------------------------------------------------
    // Request helpers
    // -------------------------------------------------------------------

    private static Task<HttpResponseMessage> PostEditAsync(HttpClient client, int boardId, string name, params (string Key, string Value)[] fields)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("Id", boardId.ToString()),
            new("Name", name)
        };
        form.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

        return client.PostAsync("/platform/Group/Edit", new FormUrlEncodedContent(form), Token);
    }

    // The posted shape of one switch row: the page name and the value the checkbox would send.
    private static (string, string)[] Row(int index, BoardPage page, bool enabled) =>
    [
        ($"Pages[{index}].Page", page.ToString()),
        ($"Pages[{index}].Enabled", enabled ? "true" : "false")
    ];

    private async Task<(int Disabled, int Home)> StoredSettingsAsync(int boardId)
    {
        await using var ctx = _factory.Database.CreateContext();
        var group = await ctx.Groups.SingleAsync(g => g.Id == boardId, Token);
        return (group.DisabledPages, group.HomePage);
    }

    private static async Task<string> ReadDecodedAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));

    private static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, Token);
        return (response.StatusCode, await ReadDecodedAsync(response));
    }

    private static async Task<string> GetOkAsync(HttpClient client, string url, string userAgent)
    {
        var (status, html) = await GetAsync(client, url, userAgent);
        status.Should().Be(HttpStatusCode.OK);
        return html;
    }

    // A link or click target to the page, not a longer id that merely starts with it.
    private static bool Links(string html, string url) =>
        Regex.IsMatch(html, Regex.Escape(url) + @"(?!\d)");

    private static string QuestUrl(int id) => $"/Quest/Details/{id}";

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

    private static string SwitchInput(string html, BoardPage page)
    {
        var match = Regex.Match(html, $"<input[^>]*id=\"page-{page}\"[^>]*>");
        match.Success.Should().BeTrue($"the form should carry a switch for {page}");
        return match.Value;
    }

    // -------------------------------------------------------------------
    // The Platform form
    // -------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task EditForm_ShowsAQuestListSwitchRow(string userAgent)
    {
        await SeedAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var html = await GetOkAsync(client, $"/platform/Group/Edit/{BoardA}", userAgent);

        html.Should().MatchRegex(@"form-check-label[^>]*>\s*Quest list\s*</label>");
        var input = SwitchInput(html, BoardPage.Quests);
        input.Should().Contain("checked");
        input.Should().NotContain("disabled");
    }

    [Fact]
    public async Task Edit_SwitchingQuestsOff_WithAnOpenQuestWhoseDatesAllPassed_IsRefusedAndNothingIsSaved()
    {
        var scenario = await SeedAsync();
        await BoardMoveSeed.SeedQuestAsync(
            _factory, BoardA, "Votes Never Closed", scenario.Dm.Id, [new DateTime(2020, 3, 1, 19, 0, 0)]);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await PostEditAsync(client, BoardA, "EuphoriaInn",
            [.. Row(0, BoardPage.Quests, false), ("HomePage", nameof(BoardPage.Calendar))]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("1 live quest");
        var stored = await StoredSettingsAsync(BoardA);
        stored.Disabled.Should().Be(0);
        stored.Home.Should().Be(0);
    }

    [Fact]
    public async Task Edit_SwitchingQuestsOff_WithASecondLiveQuest_CountsBothAndUsesThePlural()
    {
        var scenario = await SeedAsync();
        await BoardMoveSeed.SeedQuestAsync(_factory, BoardA, "Open One", scenario.Dm.Id, [new DateTime(2020, 3, 1, 19, 0, 0)]);
        await BoardMoveSeed.SeedQuestAsync(
            _factory, BoardA, "Booked Next Year", scenario.Dm.Id, [new DateTime(2099, 1, 1, 19, 0, 0)],
            finalizedDate: new DateTime(2099, 1, 1, 19, 0, 0));
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await PostEditAsync(client, BoardA, "EuphoriaInn",
            [.. Row(0, BoardPage.Quests, false), ("HomePage", nameof(BoardPage.Calendar))]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("2 live quests");
        (await StoredSettingsAsync(BoardA)).Disabled.Should().Be(0);
    }

    [Fact]
    public async Task Edit_SwitchingQuestsOff_WithOnlyAClosedAndAPlayedQuest_Saves()
    {
        await SeedAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await PostEditAsync(client, BoardA, "EuphoriaInn",
            [.. Row(0, BoardPage.Quests, false), ("HomePage", nameof(BoardPage.Calendar))]);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var stored = await StoredSettingsAsync(BoardA);
        stored.Disabled.Should().Be(QuestsBit);
        stored.Home.Should().Be((int)BoardPage.Calendar);
    }

    [Fact]
    public async Task Edit_SwitchingQuestsOff_WhileTheQuestListIsTheHome_IsRefusedAndNothingIsSaved()
    {
        await SeedAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await PostEditAsync(client, BoardA, "EuphoriaInn", Row(0, BoardPage.Quests, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("Quest list is this board's home page; pick another home page first.");
        (await StoredSettingsAsync(BoardA)).Disabled.Should().Be(0);
    }

    [Fact]
    public async Task Edit_LiveQuestsOnTheActiveBoard_DoNotBlockSwitchingQuestsOffOnAnotherBoard()
    {
        var scenario = await SeedAsync();
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardB, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.SeedQuestAsync(_factory, BoardA, "Live On Alpha", scenario.Dm.Id, [new DateTime(2099, 1, 1, 19, 0, 0)]);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await PostEditAsync(client, BoardB, "Board Bravo",
            [.. Row(0, BoardPage.Quests, false), ("HomePage", nameof(BoardPage.Calendar))]);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await StoredSettingsAsync(BoardB)).Disabled.Should().Be(QuestsBit);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task EditForm_QuestListOff_ShowsTheQuestLogRowLockedAndTheQuestListUnticked(string userAgent)
    {
        await SeedAsync();
        await SwitchQuestsOffAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var html = await GetOkAsync(client, $"/platform/Group/Edit/{BoardA}", userAgent);

        html.Should().Contain("Needs the quest list");
        SwitchInput(html, BoardPage.QuestLog).Should().Contain("disabled");
        SwitchInput(html, BoardPage.Quests).Should().NotContain("checked");
    }

    // -------------------------------------------------------------------
    // Every way into the quest pages
    // -------------------------------------------------------------------

    [Theory]
    [InlineData("player", "/quests", QuestListToast)]
    [InlineData("player", "/Quest/Details/{closed}", QuestListToast)]
    [InlineData("player", "/QuestLog", QuestLogToast)]
    [InlineData("player", "/QuestLog/Details/{closed}", QuestLogToast)]
    [InlineData("dm", "/Quest/Create", QuestListToast)]
    [InlineData("dm", "/Quest/Manage/{closed}", QuestListToast)]
    [InlineData("admin", "/Admin/Quests", QuestListToast)]
    [InlineData("superadmin", "/Move/Quest/{closed}", QuestListToast)]
    public async Task QuestsOff_EveryWayIntoTheQuestPages_RedirectsToTheBoardsHomeWithTheToast(string role, string urlTemplate, string toast)
    {
        var scenario = await SeedAsync();
        await SwitchQuestsOffAsync();
        var client = await CreateClientAsync(role);

        var response = await client.GetAsync(urlTemplate.Replace("{closed}", scenario.ClosedQuestId.ToString()), Token);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/Calendar");
        var landed = await client.GetAsync(response.Headers.Location, Token);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(landed)).Should().Contain(toast);
    }

    private async Task<HttpClient> CreateClientAsync(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return role switch
        {
            "dm" => (await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory, $"qo_dm_{suffix}", $"qo_dm_{suffix}@example.com")).client,
            "admin" => (await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory, $"qo_admin_{suffix}", $"qo_admin_{suffix}@example.com")).client,
            "superadmin" => (await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory, $"qo_super_{suffix}", $"qo_super_{suffix}@example.com")).client,
            _ => (await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, $"qo_player_{suffix}", $"qo_player_{suffix}@example.com")).client
        };
    }

    [Fact]
    public async Task QuestsOn_TheSameQuestPagesAreServed()
    {
        var scenario = await SeedAsync();
        var player = await CreateClientAsync("player");

        (await player.GetAsync("/quests", Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await player.GetAsync(QuestUrl(scenario.ClosedQuestId), Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await player.GetAsync("/QuestLog", Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------
    // Menus
    // -------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOff_DungeonMasterMenu_HasNoCreateQuest(string userAgent)
    {
        await SeedAsync();
        await SwitchQuestsOffAsync();
        var dm = await CreateClientAsync("dm");

        var nav = NavigationOf(await GetOkAsync(dm, "/Agenda", userAgent));

        nav.Should().NotContain("Create Quest");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOff_AdminMenu_HasNoQuestManagement(string userAgent)
    {
        await SeedAsync();
        await SwitchQuestsOffAsync();
        var admin = await CreateClientAsync("admin");

        var nav = NavigationOf(await GetOkAsync(admin, "/Agenda", userAgent));

        nav.Should().Contain("User Management");
        nav.Should().NotContain("Quest Management");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOff_PlayerMenu_HasNoQuestLogNoQuestsItemAndTheBrandGoesToTheHome(string userAgent)
    {
        await SeedAsync();
        await SwitchQuestsOffAsync();
        var player = await CreateClientAsync("player");

        var nav = NavigationOf(await GetOkAsync(player, "/Agenda", userAgent));

        nav.Should().NotContain("href=\"/QuestLog\"");
        nav.Should().NotContain("id=\"nav-quests");
        nav.Should().NotContain("href=\"/quests\"");
        nav.Should().Contain("href=\"/Calendar\"");
        BrandHref(nav).Should().Be("/Calendar");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOn_MenusStillCarryTheQuestEntries(string userAgent)
    {
        await SeedAsync();
        var dm = await CreateClientAsync("dm");
        var admin = await CreateClientAsync("admin");
        var player = await CreateClientAsync("player");

        NavigationOf(await GetOkAsync(dm, "/Agenda", userAgent)).Should().Contain("Create Quest");
        NavigationOf(await GetOkAsync(admin, "/Agenda", userAgent)).Should().Contain("Quest Management");
        var playerNav = NavigationOf(await GetOkAsync(player, "/Agenda", userAgent));
        playerNav.Should().Contain("href=\"/QuestLog\"");
        BrandHref(playerNav).Should().Be("/quests");
    }

    // -------------------------------------------------------------------
    // The board's own calendar and the views that list quests
    // -------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOff_OwnCalendar_LoadsNoQuestsButStillShowsEvents(string userAgent)
    {
        await SeedAsync();
        await SwitchQuestsOffAsync();
        var player = await CreateClientAsync("player");

        var html = await GetOkAsync(player, CalendarUrl, userAgent);

        html.Should().NotContain(PlayedTitle);
        html.Should().NotContain(ClosedTitle);
        html.Should().Contain(EventTitle);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOn_OwnCalendar_ShowsTheQuestAndTheEvent(string userAgent)
    {
        var scenario = await SeedAsync();
        var player = await CreateClientAsync("player");

        var html = await GetOkAsync(player, CalendarUrl, userAgent);

        html.Should().Contain(PlayedTitle);
        Links(html, QuestUrl(scenario.PlayedQuestId)).Should().BeTrue();
        html.Should().Contain(EventTitle);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOff_DungeonMasterProfile_ShowsQuestHistoryTitlesAsPlainText(string userAgent)
    {
        var scenario = await SeedAsync();
        await SwitchQuestsOffAsync();
        var player = await CreateClientAsync("player");

        var html = await GetOkAsync(player, $"/DungeonMaster/Profile/{scenario.Dm.Id}", userAgent);

        html.Should().Contain(ClosedTitle);
        html.Should().Contain(PlayedTitle);
        Links(html, QuestUrl(scenario.ClosedQuestId)).Should().BeFalse();
        Links(html, QuestUrl(scenario.PlayedQuestId)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestsOn_DungeonMasterProfile_LinksTheQuestHistoryTitles(string userAgent)
    {
        var scenario = await SeedAsync();
        var player = await CreateClientAsync("player");

        var html = await GetOkAsync(player, $"/DungeonMaster/Profile/{scenario.Dm.Id}", userAgent);

        html.Should().Contain(ClosedTitle);
        Links(html, QuestUrl(scenario.ClosedQuestId)).Should().BeTrue();
        Links(html, QuestUrl(scenario.PlayedQuestId)).Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // A linked board that switched its quests off
    // -------------------------------------------------------------------

    private async Task<(HttpClient Member, int QuestId)> SeedLinkedAsync()
    {
        var scenario = await SeedAsync();
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardB, "Linked Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(_factory, BoardType.OneShot, BoardA, BoardB);

        var (member, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "questsoff_member", "questsoff_member@example.com", roles: []);
        await BoardMoveSeed.SeedMembershipAsync(_factory, user.Id, BoardA, GroupRole.Player);
        await BoardMoveSeed.SeedMembershipAsync(_factory, user.Id, BoardB, GroupRole.Player);

        _factory.TestGroupContext.ActiveGroupId = BoardB;
        return (member, scenario.PlayedQuestId);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Linked_OwningBoardWithQuestsOff_ItsQuestIsPlainTextOnTheOtherBoardsCalendar(string userAgent)
    {
        var (member, questId) = await SeedLinkedAsync();
        await SwitchQuestsOffAsync();

        var html = await GetOkAsync(member, CalendarUrl, userAgent);

        html.Should().Contain(PlayedTitle);
        Links(html, QuestUrl(questId)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Linked_OwningBoardWithQuestsOn_ItsQuestIsLinkedForAMember(string userAgent)
    {
        var (member, questId) = await SeedLinkedAsync();

        var html = await GetOkAsync(member, CalendarUrl, userAgent);

        html.Should().Contain(PlayedTitle);
        Links(html, QuestUrl(questId)).Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Switching back on
    // -------------------------------------------------------------------

    [Fact]
    public async Task SwitchingQuestsBackOn_BringsEveryQuestBackUnchanged()
    {
        var scenario = await SeedAsync();
        await SwitchQuestsOffAsync();
        var (superAdmin, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var player = await CreateClientAsync("player");
        (await player.GetAsync("/quests", Token)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var response = await PostEditAsync(superAdmin, BoardA, "EuphoriaInn", Row(0, BoardPage.Quests, true));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await StoredSettingsAsync(BoardA)).Disabled.Should().Be(0);
        (await player.GetAsync("/quests", Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await player.GetAsync(QuestUrl(scenario.ClosedQuestId), Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var log = await player.GetAsync("/QuestLog", Token);
        log.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(log)).Should().Contain(ClosedTitle);

        var stored = await BoardMoveSeed.ReadQuestAsync(_factory, scenario.ClosedQuestId);
        stored.Title.Should().Be(ClosedTitle);
        stored.IsClosed.Should().BeTrue();
        stored.FinalizedDate.Should().Be(ClosedDate);
    }

    [Fact]
    public async Task QuestsOffAndOn_KeepsTheStoredQuestLogSettingAsItWas()
    {
        await SeedAsync();
        await BoardPageSeed.SetAsync(_factory, BoardA, BoardPageSet.Empty.With(BoardPage.QuestLog), BoardPage.Calendar);
        var (superAdmin, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var off = await PostEditAsync(superAdmin, BoardA, "EuphoriaInn",
            [.. Row(0, BoardPage.Quests, false), .. Row(1, BoardPage.QuestLog, true)]);

        off.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await StoredSettingsAsync(BoardA)).Disabled.Should().Be(QuestsBit | QuestLogBit);

        var on = await PostEditAsync(superAdmin, BoardA, "EuphoriaInn", Row(0, BoardPage.Quests, true));

        on.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await StoredSettingsAsync(BoardA)).Disabled.Should().Be(QuestLogBit);
    }
}
