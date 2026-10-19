using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// What the Move page says a move will do, on the desktop and the phone layout. Seeded: board 1
/// "Board Alpha" (active) and board 2 "Board Bravo" linked to it. The quest scenario is the one
/// the people tests use: a finalized quest with two kept members (one selected with a character,
/// one waitlisted), two people who are not on board 2 (a selected player and a spectator) and
/// both chain links.
///
/// What these facts do NOT establish: how the page looks in a browser. Wrapping at phone width
/// and the target picker's layout need a person with a browser.
/// </summary>
public class BoardMoveViewsTests(MoveWebApplicationFactory factory)
    : IClassFixture<MoveWebApplicationFactory>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const string EventTitle = "Harvest Fair";

    private static readonly DateOnly Upcoming = new(2031, 5, 10);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    public static TheoryData<string> UserAgents => new() { DesktopUserAgent, MobileUserAgent };

    private async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // The text a reader sees: tags removed and whitespace collapsed, so a sentence that wraps a
    // board name in emphasis still reads as one sentence.
    private static string Plain(string html) =>
        Regex.Replace(Regex.Replace(html, "<[^>]+>", string.Empty), @"\s+", " ");

    private static void ShouldAppearInOrder(string html, params string[] fragments)
    {
        html = Plain(html);
        var from = 0;
        foreach (var fragment in fragments)
        {
            var at = html.IndexOf(fragment, from, StringComparison.Ordinal);
            at.Should().BeGreaterThanOrEqualTo(0, $"'{fragment}' should appear, in order, after position {from}");
            from = at + fragment.Length;
        }
    }

    // A quest on board 1 owned by a Dungeon Master on both boards, plus a super admin to read the page.
    private async Task<(int QuestId, int DmId, HttpClient Client)> SeedQuestBasicsAsync(
        string key, DateTime? finalizedDate = null)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha, BoardBravo);

        var dm = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);

        var date = new DateTime(2031, 3, 10, 19, 0, 0);
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", dm.Id, [date], finalizedDate: finalizedDate);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, $"{key}_super", $"{key}_super@example.com");
        return (questId, dm.Id, client);
    }

    private async Task<int> SeedDroppedSignupAsync(
        int questId, string key, string name, SignupRole role, bool isSelected)
    {
        var user = await BoardMoveSeed.CreateUserAsync(factory, key, name);
        var dateId = (await BoardMoveSeed.ProposedDateIdsAsync(factory, questId)).First();
        return await BoardMoveSeed.SeedSignupAsync(factory, questId, user.Id, role, isSelected, null,
            new Dictionary<int, VoteType> { [dateId] = VoteType.Yes }, DateTime.UtcNow);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task QuestPage_ListsEveryConsequenceInOrderWithTheContractCopy(string userAgent)
    {
        var scenario = await BoardMoveQuestPeopleTests.SeedScenarioAsync(factory, "views_full");

        var html = await GetAsync(scenario.ActorClient, $"/Move/Quest/{scenario.QuestId}", userAgent);

        ShouldAppearInOrder(html,
            "2 signups stay on the quest, with their role, selection and date votes.",
            "Characters are cleared, because a character belongs to one board. Players pick one from Board Bravo again on the quest page.",
            "2 signups will be removed, with their date votes, because those players are not members of Board Bravo:",
            "Dropped One",
            "Selected",
            "Dropped Two",
            "(Spectator)",
            "Add them to Board Bravo first if you want to keep them, then come back.",
            "Players marked Selected hold a seat.",
            "1 seat opens up.",
            "The waitlist moves up in its usual order, and promoted players are emailed.",
            "This quest will no longer be linked to its original quest, \"Part One\".",
            "This quest will no longer be linked to its follow-up quest, \"Part Three\".",
            "Unlinking is permanent. Moving the quest back does not restore the link.",
            "In calendar apps, anyone who cannot see Board Bravo stops seeing this quest on the next refresh.",
            "The Dungeon Master and every player who stays are emailed that the quest has moved.");

        html.Should().Contain("aria-label=\"People who will be removed\"");
        html.Should().Contain("move-person-mark");
        Regex.Matches(html, "Unlinking is permanent").Count.Should().Be(1);
        Regex.Matches(html, "class=\"move-person\"").Count.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task OneDroppedPlayerWhoIsNotSelected_ShowsTheSingularSentenceAndNoSeatHint(string userAgent)
    {
        var (questId, _, client) = await SeedQuestBasicsAsync("views_single");
        await SeedDroppedSignupAsync(questId, "views_single_stranger", "Lone Stranger", SignupRole.Player, false);

        var html = await GetAsync(client, $"/Move/Quest/{questId}", userAgent);

        Plain(html).Should().Contain("1 signup will be removed, with its date votes, because that player is not a member of Board Bravo:");
        Regex.Matches(html, "class=\"move-person\"").Count.Should().Be(1);
        Plain(html).Should().Contain("Lone Stranger");
        Plain(html).Should().NotContain("Players marked Selected hold a seat.");
        html.Should().NotContain("move-person-mark");
        Plain(html).Should().NotContain("seat opens up");
        Plain(html).Should().NotContain("(Spectator)");
        Plain(html).Should().NotContain("(Assistant DM)");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task AnAssistantDmWhoIsDropped_IsAnnotatedWithTheirRole(string userAgent)
    {
        var (questId, _, client) = await SeedQuestBasicsAsync("views_adm");
        await SeedDroppedSignupAsync(questId, "views_adm_stranger", "Helper Hannah", SignupRole.AssistantDM, false);

        var html = await GetAsync(client, $"/Move/Quest/{questId}", userAgent);

        Plain(html).Should().Contain("Helper Hannah");
        Plain(html).Should().Contain("(Assistant DM)");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task AFinalizedQuestWhoseSelectedPlayerIsDroppedWithNobodyWaiting_SaysTheSeatStaysOpen(string userAgent)
    {
        var (questId, _, client) = await SeedQuestBasicsAsync("views_seat", new DateTime(2031, 3, 10, 19, 0, 0));
        await SeedDroppedSignupAsync(questId, "views_seat_stranger", "Lone Stranger", SignupRole.Player, true);

        var html = await GetAsync(client, $"/Move/Quest/{questId}", userAgent);

        Plain(html).Should().Contain("1 seat opens up.");
        Plain(html).Should().Contain("Nobody is on the waitlist, so the seat stays open.");
        Plain(html).Should().NotContain("The waitlist moves up in its usual order");
        Plain(html).Should().Contain("Players marked Selected hold a seat.");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task ANothingToRemoveQuest_ShowsOnlyTheKeptCalendarAndEmailRowsAndNoZeroCounts(string userAgent)
    {
        var (questId, _, client) = await SeedQuestBasicsAsync("views_none");

        var html = await GetAsync(client, $"/Move/Quest/{questId}", userAgent);

        Regex.Matches(html, "class=\"move-consequence move-consequence--").Count.Should().Be(3);
        Plain(html).Should().Contain("No signups to keep.");
        Plain(html).Should().Contain("In calendar apps, anyone who cannot see Board Bravo stops seeing this quest on the next refresh.");
        Plain(html).Should().Contain("The Dungeon Master and every player who stays are emailed that the quest has moved.");
        Plain(html).Should().NotContain("will be removed");
        Plain(html).Should().NotContain("Characters are cleared");
        Plain(html).Should().NotContain("no longer be linked");
        Plain(html).Should().NotContain("0 signups");
        Plain(html).Should().NotContain("0 seats");
        html.Should().NotContain("aria-label=\"People who will be removed\"");
    }

    [Fact]
    public async Task ACampaignTargetQuest_HasNoCalendarAppsRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.Campaign);
        await BoardMoveSeed.LinkAsync(factory, BoardType.Campaign, BoardAlpha, BoardBravo);
        var dm = await BoardMoveSeed.CreateUserAsync(factory, "views_camp_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", dm.Id, [new DateTime(2031, 3, 10, 19, 0, 0)]);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "views_camp_super", "views_camp_super@example.com");

        var html = await GetAsync(client, $"/Move/Quest/{questId}", DesktopUserAgent);

        Plain(html).Should().Contain("What will happen on Board Bravo");
        Plain(html).Should().NotContain("In calendar apps");
    }

    [Fact]
    public async Task TheDesktopLayoutLinksTheDesktopSheetOnly_AndTheMobileLayoutTheMobileSheetOnly()
    {
        var (questId, _, client) = await SeedQuestBasicsAsync("views_css");

        var desktop = await GetAsync(client, $"/Move/Quest/{questId}", DesktopUserAgent);
        var mobile = await GetAsync(client, $"/Move/Quest/{questId}", MobileUserAgent);

        desktop.Should().Contain("/css/move-entry.css");
        desktop.Should().NotContain("move-entry.mobile.css");
        mobile.Should().Contain("/css/move-entry.mobile.css");
        mobile.Should().NotContain("/css/move-entry.css");
        mobile.Should().Contain("move-card-mobile");
    }

    private async Task<(int EventId, HttpClient Client)> SeedEventBasicsAsync(
        string key, BoardType type, TimeOnly? startTime = null, DateTime? cancelledAt = null)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = type;
        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", type);
        await BoardMoveSeed.LinkAsync(factory, type, BoardAlpha, BoardBravo);
        var eventId = await BoardMoveSeed.SeedEventAsync(
            factory, BoardAlpha, EventTitle, Upcoming, startTime, cancelledAt: cancelledAt);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, $"{key}_super", $"{key}_super@example.com");
        return (eventId, client);
    }

    private async Task<UserEntity> SeedMemberOfBravoAsync(string key, string name)
    {
        var user = await BoardMoveSeed.CreateUserAsync(factory, key, name);
        await BoardMoveSeed.SeedMembershipAsync(factory, user.Id, BoardBravo, GroupRole.Player);
        return user;
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task EventPage_OnACampaignTarget_ListsKeptRemovedAndAutomaticRowsInOrder(string userAgent)
    {
        var (eventId, client) = await SeedEventBasicsAsync("views_evt_camp", BoardType.Campaign);
        var answered = await SeedMemberOfBravoAsync("views_evt_answered", "Answered Member");
        await SeedMemberOfBravoAsync("views_evt_silent_one", "Silent One");
        await SeedMemberOfBravoAsync("views_evt_silent_two", "Silent Two");
        var stranger = await BoardMoveSeed.CreateUserAsync(factory, "views_evt_stranger", "Wandering Stranger");
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, answered.Id, VoteType.Maybe, DateTime.UtcNow);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, stranger.Id, VoteType.Yes, DateTime.UtcNow);

        var html = await GetAsync(client, $"/Move/Event/{eventId}", userAgent);

        ShouldAppearInOrder(html,
            "1 availability answer stays exactly as given.",
            "1 availability answer will be removed because that person is not a member of Board Bravo:",
            "Wandering Stranger",
            "Add them to Board Bravo first if you want to keep them, then come back.",
            "2 members of Board Bravo have no answer yet and get an automatic Yes, like any new Campaign event.",
            "In calendar apps, anyone who cannot see Board Bravo stops seeing this event on the next refresh.",
            "No emails are sent for an event move.");
        Plain(html).Should().NotContain("Players marked Selected hold a seat.");
        html.Should().NotContain("move-person-mark");
        Plain(html).Should().NotContain("(Spectator)");
        Plain(html).Should().NotContain("This session is cancelled");
    }

    [Fact]
    public async Task EventPage_OnAOneShotTarget_HasNoCampaignRow()
    {
        var (eventId, client) = await SeedEventBasicsAsync("views_evt_one", BoardType.OneShot);
        await SeedMemberOfBravoAsync("views_evt_one_member", "Silent Member");

        var html = await GetAsync(client, $"/Move/Event/{eventId}", DesktopUserAgent);

        Plain(html).Should().Contain("No availability answers to keep.");
        Plain(html).Should().NotContain("automatic Yes");
        Plain(html).Should().NotContain("will be removed");
        Regex.Matches(html, "class=\"move-consequence move-consequence--").Count.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task ACancelledEvent_SaysItStaysCancelledAndIsMarkedCancelledInTheSummary(string userAgent)
    {
        var (eventId, client) = await SeedEventBasicsAsync(
            "views_evt_cancel", BoardType.OneShot, cancelledAt: new DateTime(2031, 1, 5, 9, 0, 0, DateTimeKind.Utc));

        var html = await GetAsync(client, $"/Move/Event/{eventId}", userAgent);

        Plain(html).Should().Contain("This session is cancelled and stays cancelled on Board Bravo.");
        Plain(html).Should().Contain("Cancelled");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task TheEventSummary_ShowsTheDateAsAWallClockAndTheTimeLabel(string userAgent)
    {
        var (allDayId, client) = await SeedEventBasicsAsync("views_evt_allday", BoardType.OneShot);

        var allDay = await GetAsync(client, $"/Move/Event/{allDayId}", userAgent);

        allDay.Should().Contain("class=\"wall-clock\"");
        allDay.Should().Contain("datetime=\"2031-05-10T00:00\"");
        Plain(allDay).Should().Contain("All day");
        Plain(allDay).Should().NotContain("This session is cancelled");

        var (timedId, timedClient) = await SeedEventBasicsAsync(
            "views_evt_timed", BoardType.OneShot, new TimeOnly(19, 30));

        var timed = await GetAsync(timedClient, $"/Move/Event/{timedId}", userAgent);

        Plain(timed).Should().Contain("19:30");
        Plain(timed).Should().NotContain("All day");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task AQuestWhoseDmIsOnlyAPlayerOnTheTarget_ShowsTheRefusalAndNoConfirmForm(string userAgent)
    {
        var scenario = await BoardMoveQuestPeopleTests.SeedScenarioAsync(
            factory, "views_refuse", dmRoleOnBravo: GroupRole.Player);

        var html = await GetAsync(scenario.ActorClient, $"/Move/Quest/{scenario.QuestId}", userAgent);

        Plain(html).Should().Contain("Board Bravo cannot take this quest.");
        Plain(html).Should().Contain("Dana the DM is not a Dungeon Master or Admin on Board Bravo. Make them one there, or choose another board.");
        html.Should().Contain("alert alert-danger");
        html.Should().NotContain("action=\"/Move/Quest\"");
        html.Should().NotContain("name=\"stamp\"");
        Plain(html).Should().NotContain("Characters are cleared");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task APersonNamedWithMarkup_IsRenderedEncodedInTheRemovedList(string userAgent)
    {
        var (questId, _, client) = await SeedQuestBasicsAsync("views_xss");
        await SeedDroppedSignupAsync(questId, "views_xss_stranger", "<i>Sneaky</i>", SignupRole.Player, false);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Move/Quest/{questId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        raw.Should().Contain("&lt;i&gt;Sneaky&lt;/i&gt;");
        raw.Should().NotContain("<i>Sneaky</i>");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, normalized);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"'{relativePath}' not found searching upward from '{AppContext.BaseDirectory}'.", relativePath);
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/Move/Index.cshtml")]
    [InlineData("QuestBoard.Service/Views/Move/Index.Mobile.cshtml")]
    public void TheMoveViews_KeepScriptInlineStyleAndTheTrapClassesOut(string path)
    {
        var source = ReadRepoFile(path);

        source.Should().NotContain("style=\"");
        source.Should().NotContain("<script");
        source.Should().NotContain("text-muted");
        source.Should().NotContain("form-text");
        source.Should().NotContain("fw-bold");
        source.Should().NotContain("Html.Raw");
    }

    [Fact]
    public void TheDesktopMoveView_HasNoSpan()
    {
        var source = ReadRepoFile("QuestBoard.Service/Views/Move/Index.cshtml");

        source.Should().NotContain("<span");
    }

    [Theory]
    [InlineData("QuestBoard.Service/wwwroot/css/move-entry.css")]
    [InlineData("QuestBoard.Service/wwwroot/css/move-entry.mobile.css")]
    public void TheMoveStylesheets_HaveNoMediaQuery(string path)
    {
        var source = ReadRepoFile(path);

        source.Should().NotContain("@media");
    }
}
