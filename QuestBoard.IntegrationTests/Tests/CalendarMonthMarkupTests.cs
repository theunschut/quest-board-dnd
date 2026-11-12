using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The desktop Calendar month as the server writes it: one compact row per entry, in a fixed order, with
/// the full accessible name and a hidden detail block, linked entries that stay inert unless the viewer
/// belongs to the board, a week count the stylesheet can size the rows by, the board's own today, the
/// page header with its legend and named month buttons, and the one-line note of an empty month.
/// Quest Details draws the same partial and must keep its vote layout.
/// </summary>
public class CalendarMonthMarkupTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const int BoardA = 1;
    private const int BoardB = 2;
    private const string BoardBName = "Month Linked Bravo";

    // A fixed month far from the present, so no fact depends on what day it is when it runs. March 2031
    // starts on a Saturday and spans six weeks; February 2031 spans five.
    private const int Year = 2031;
    private const int Month = 3;
    private const string CalendarUrl = "/Calendar?year=2031&month=3";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = BoardA;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static async Task<string> GetAsync(HttpClient client, string url = CalendarUrl)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", DesktopUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The markup of one day cell, found by the day-number element it holds.
    private static string DayCell(string html, int day)
    {
        var number = new Regex($"class=\"day-number\"[^>]*>\\s*{day}\\s*<");
        return Regex.Split(html, "class=\"calendar-day(?=[\" ])").Single(segment => number.IsMatch(segment));
    }

    // The list items of one day cell, one string per entry row.
    private static List<string> Rows(string cell) =>
        Regex.Matches(cell, "<li class=\"cal-entry[\\s\\S]*?</li>").Select(m => m.Value).ToList();

    // The opening tag of a row, so hook classes are read from the row itself.
    private static string OpeningTag(string row) => row[..(row.IndexOf('>') + 1)];

    private async Task<(HttpClient Client, UserEntity Viewer)> CreateViewerAsync(string name, params (int GroupId, GroupRole Role)[] memberships)
    {
        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, $"{name}@example.com", roles: []);
        foreach (var (groupId, role) in memberships)
        {
            await BoardMoveSeed.SeedMembershipAsync(factory, viewer.Id, groupId, role);
        }
        return (client, viewer);
    }

    private async Task SeedLinkedPairAsync()
    {
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await BoardMoveSeed.SeedBoardAsync(factory, BoardB, BoardBName, BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardA, BoardB);
    }

    private async Task<int> SeedQuestAsync(
        int groupId, string title, string dmName, int[] proposedDays, bool finalized = false, bool dmHasKey = false,
        Func<List<ProposedDateEntity>, List<PlayerSignupEntity>>? signups = null)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "monthmarkup_dm", "monthmarkup_dm@example.com", name: dmName);

        await using var ctx = factory.Database.CreateContext();
        if (dmHasKey)
        {
            var tracked = await ctx.Users.FindAsync([dm.Id], TestContext.Current.CancellationToken);
            tracked!.HasKey = true;
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var dates = proposedDays.Select(d => new ProposedDateEntity { Date = new DateTime(Year, Month, d, 19, 0, 0) }).ToList();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Month markup test quest",
            ChallengeRating = 5,
            DungeonMasterId = dm.Id,
            TotalPlayerCount = 4,
            GroupId = groupId,
            IsFinalized = finalized,
            FinalizedDate = finalized ? new DateTime(Year, Month, proposedDays[0], 19, 0, 0) : null,
            CreatedAt = DateTime.UtcNow,
            ProposedDates = dates
        };
        if (signups is not null)
        {
            quest.PlayerSignups = signups(dates);
        }
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    private Task<int> SeedEventAsync(int groupId, string title, int day, TimeOnly? startTime = null, bool cancelled = false) =>
        BoardMoveSeed.SeedEventAsync(
            factory, groupId, title, new DateOnly(Year, Month, day), startTime,
            cancelledAt: cancelled ? DateTime.UtcNow : null);

    // ---- Entries ----

    [Fact]
    public async Task Cells_ListEventsThenQuestsThenLinkedEntries()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        await SeedQuestAsync(BoardA, "Order Quest Own", "Dungeon Master Dana", [12]);
        await SeedEventAsync(BoardA, "Order Event Own", 12, new TimeOnly(18, 0));
        await SeedEventAsync(BoardB, "Order Linked Event", 12, new TimeOnly(20, 0));
        await SeedQuestAsync(BoardB, "Order Linked Quest", "Dungeon Master Dana", [12]);
        var (client, _) = await CreateViewerAsync("monthmarkup_order", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));

        var cell = DayCell(await GetAsync(client), 12);

        cell.Should().Contain("<ul class=\"cal-entries\">");
        var rows = Rows(cell);
        rows.Should().HaveCount(4);
        OpeningTag(rows[0]).Should().Contain("calendar-event");
        OpeningTag(rows[1]).Should().Contain("quest-event proposed");
        OpeningTag(rows[2]).Should().Contain("linked-entry linked-entry--event");
        OpeningTag(rows[3]).Should().Contain("linked-entry linked-entry--quest linked-entry--proposed");
        rows.Should().OnlyContain(row => Regex.Matches(row, "class=\"[^\"]*cal-entry-detail[^\"]*\" hidden").Count == 1,
            because: "each row carries exactly one hidden detail block");
        rows[0].Should().Contain("Order Event Own");
        rows[1].Should().Contain("Order Quest Own");
    }

    [Fact]
    public async Task OwnEntries_CarryTheFullAccessibleName()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, viewer) = await CreateViewerAsync("monthmarkup_names", (BoardA, GroupRole.Player));
        await SeedQuestAsync(BoardA, "Name Quest Yes", "Dungeon Master Dana", [10],
            signups: dates => [new PlayerSignupEntity
            {
                PlayerId = viewer.Id,
                DateVotes = [new PlayerDateVoteEntity { ProposedDate = dates[0], Vote = (int)VoteType.Yes }]
            }]);
        await SeedQuestAsync(BoardA, "Name Quest Keyed", "Dungeon Master Dana", [14], finalized: true, dmHasKey: true);
        await SeedEventAsync(BoardA, "Name Event Timed", 16, new TimeOnly(18, 30));
        await SeedEventAsync(BoardA, "Name Event Called Off", 18, new TimeOnly(17, 0), cancelled: true);
        await SeedEventAsync(BoardA, "Name Event All Day", 20);

        var html = await GetAsync(client);

        var quest = DayCell(html, 10);
        quest.Should().Contain(", Proposed, 19:00, DM Dungeon Master Dana, No building key available, Your vote: Yes");
        quest.Should().Contain("cal-mark--nokey");
        quest.Should().Contain("<p class=\"cal-detail__note\"><i class=\"fas fa-check\" aria-hidden=\"true\"></i>Your vote: Yes</p>",
            because: "the viewer's own answer is part of the detail block");
        Rows(quest).Single().Should().NotContain("cal-mark--vote-yes",
            because: "the row keeps only the building-key marker; the answer is in the name and the detail block");

        var keyed = DayCell(html, 14);
        keyed.Should().Contain(", Finalized, 19:00, DM Dungeon Master Dana</span>");
        keyed.Should().NotContain("No building key");
        keyed.Should().Contain("cal-mark--finalized");

        DayCell(html, 16).Should().Contain(", Event, 18:30</span>");
        var calledOff = DayCell(html, 18);
        calledOff.Should().Contain(", Event, 17:00, Cancelled</span>");
        calledOff.Should().Contain("cal-mark--cancelled");

        var allDay = DayCell(html, 20);
        allDay.Should().Contain(", Event, All day</span>");
        allDay.Should().NotContain("class=\"cal-entry__time\"", because: "an all-day event has no time column on its row");
    }

    [Fact]
    public async Task StaticLinkedEntries_StayInert()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        await SeedEventAsync(BoardB, "Inert Linked Event", 12, new TimeOnly(19, 0));
        var (client, _) = await CreateViewerAsync("monthmarkup_inert", (BoardA, GroupRole.Player));

        var row = Rows(DayCell(await GetAsync(client), 12)).Single();

        OpeningTag(row).Should().Contain("linked-entry--static");
        OpeningTag(row).Should().Contain($"title=\"Inert Linked Event - {BoardBName} - 19:00\"");
        row.Should().NotContain("href=");
        row.Should().NotContain("tabindex");
        row.Should().NotContain("data-");
        row.Should().NotContain("<a ");
        row.Should().Contain($"<span class=\"visually-hidden\">{BoardBName}. 19:00.</span>");
        row.Should().Contain("<div class=\"cal-entry__body\">");
    }

    [Fact]
    public async Task MemberLinkedEntries_AreOneLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        var eventId = await SeedEventAsync(BoardB, "Member Linked Event", 12, new TimeOnly(19, 0));
        var (client, _) = await CreateViewerAsync("monthmarkup_member", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));

        var row = Rows(DayCell(await GetAsync(client), 12)).Single();

        OpeningTag(row).Should().Contain("linked-entry--open");
        Regex.Matches(row, "<a ").Should().HaveCount(1);
        row.Should().Contain($"<a class=\"cal-entry__body\" href=\"/Events/Details/{eventId}\">");
        row.Should().Contain("fa-link cal-entry__link-glyph", because: "a member's row carries the link glyph");
    }

    // ---- Grid ----

    [Theory]
    [InlineData(2027, 2, 4)]
    [InlineData(2031, 2, 5)]
    [InlineData(2031, 3, 6)]
    public async Task Grid_CarriesItsWeekCount(int year, int month, int weeks)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await CreateViewerAsync("monthmarkup_weeks", (BoardA, GroupRole.Player));

        var html = await GetAsync(client, $"/Calendar?year={year}&month={month}");

        html.Should().Contain($"class=\"calendar-grid calendar-grid--weeks-{weeks} ");
        Regex.Matches(html, "class=\"calendar-day[ \"]").Should().HaveCount(weeks * 7);
    }

    // ---- Today ----

    private static readonly DateTimeOffset PinnedNow = new(2026, 9, 5, 23, 30, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task TodayCell_IsMarkedFromTheBoardClock()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        // At 23:30 UTC on the 5th the board (Europe/Amsterdam) is already on the 6th.
        var pinned = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["TimeZone:BoardTimeZoneId"] = "Europe/Amsterdam" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(PinnedNow));
            });
        });
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            pinned, "monthmarkup_today", "monthmarkup_today@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Calendar?year=2026&month=9");
        request.Headers.TryAddWithoutValidation("User-Agent", DesktopUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var html = await (await client.SendAsync(request, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var sixth = DayCell(html, 6);
        sixth[..sixth.IndexOf('>')].Should().Contain("today").And.Contain("aria-current=\"date\"");
        var hidden = Regex.Match(sixth, "<span class=\"visually-hidden\">([^<]*)<time").Groups[1].Value;
        hidden.Should().Be("Today. ", because: "the hidden word comes before the date text");

        var fifth = DayCell(html, 5);
        fifth[..fifth.IndexOf('>')].Should().NotContain("today").And.NotContain("aria-current");
        fifth.Should().NotContain("Today.");
    }

    // ---- Quest Details keeps its vote layout ----

    [Fact]
    public async Task QuestDetails_KeepsItsVoteLayoutAndNoCompactControls()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await CreateViewerAsync("monthmarkup_details", (BoardA, GroupRole.Player));
        var questId = await SeedQuestAsync(BoardA, "Details Vote Quest", "Dungeon Master Dana", [10, 17]);

        var html = await GetAsync(client, $"/Quest/Details/{questId}");

        html.Should().Contain("details-page");
        html.Should().Contain("current-quest");
        html.Should().Contain("vote-buttons");
        html.Should().Contain("vote-choice vote-choice--yes");
        html.Should().Contain("<span class=\"visually-hidden\">Yes</span>");
        html.Should().Contain("class=\"calendar-header\"");
        html.Should().NotContain("calendar-grid--weeks");
        html.Should().NotContain("cal-entries");
        html.Should().NotContain("cal-entry");
        html.Should().NotContain("cal-more");
        html.Should().NotContain("cal-card");
        html.Should().NotContain("cal-panel");
        html.Should().NotContain("calendar-day__top");
    }

    // ---- Overlay shells and the month script ----

    [Fact]
    public async Task Shells_RenderOnlyOnTheCalendarPage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await CreateViewerAsync("monthmarkup_shells", (BoardA, GroupRole.Player));
        var questId = await SeedQuestAsync(BoardA, "Shell Quest", "Dungeon Master Dana", [10, 17]);

        var month = await GetAsync(client);

        // The detail card is one empty, hidden shell the script fills.
        Regex.Matches(month, "<div class=\"cal-card\"[^>]*>").Should().ContainSingle()
            .Which.Value.Should().Contain("aria-hidden=\"true\"").And.Contain("hidden");

        // The day panel is one hidden, non-modal dialog named by its own heading, with a Close button.
        var panelStart = month.IndexOf("<div class=\"cal-panel\"", StringComparison.Ordinal);
        panelStart.Should().BeGreaterThan(-1);
        Regex.Matches(month, "class=\"cal-panel\"").Should().HaveCount(1);
        var panel = month[panelStart..month.IndexOf("</ul>", panelStart, StringComparison.Ordinal)];
        panel.Should().Contain("id=\"cal-panel\"");
        panel.Should().Contain("role=\"dialog\"");
        panel.Should().Contain("aria-modal=\"false\"");
        panel.Should().Contain("aria-labelledby=\"cal-panel-title\"");
        panel.Should().Contain("hidden");
        Regex.IsMatch(panel, "<h2 class=\"cal-panel__title\" id=\"cal-panel-title\"></h2>").Should().BeTrue();
        panel.Should().Contain("class=\"cal-panel__count\"");
        Regex.IsMatch(panel, "<button type=\"button\" class=\"cal-panel__close\" aria-label=\"Close\"><i class=\"fas fa-times\" aria-hidden=\"true\"></i></button>")
            .Should().BeTrue("the Close button is a real button with an accessible name and a hidden icon");
        panel.Should().Contain("<ul class=\"cal-panel__rows\">");

        // Both shells sit outside the month wrapper, so a blurred ancestor never holds them, even before
        // the script moves them to the body.
        var shellsStart = month.IndexOf("<div class=\"cal-card\"", StringComparison.Ordinal);
        var wrapperStart = month.IndexOf("<div class=\"calendar-page\"", StringComparison.Ordinal);
        shellsStart.Should().BeGreaterThan(wrapperStart);
        var before = month[wrapperStart..shellsStart];
        Regex.Matches(before, "<div\\b").Count.Should().Be(Regex.Matches(before, "</div>").Count,
            "the month wrapper has already closed when the shells begin");
        month.Should().MatchRegex("<script type=\"module\" src=\"/js/calendar-month\\.js");

        var details = await GetAsync(client, $"/Quest/Details/{questId}");
        details.Should().NotContain("cal-card");
        details.Should().NotContain("cal-panel");
        details.Should().NotContain("calendar-month.js");
    }

    // ---- Header, legend, empty month ----

    [Fact]
    public async Task Header_HasTitleLegendAndNamedNavigation()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        await SeedEventAsync(BoardA, "Header Night", 12, new TimeOnly(18, 0));
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "monthmarkup_header_dm", "monthmarkup_header_dm@example.com");

        var html = await GetAsync(client);
        var page = DesktopPageRegions.PageContent(html);

        Regex.Matches(page, "<h1[ >]").Should().HaveCount(1);
        Regex.IsMatch(page, @"<h1[^>]*><i class=""fas fa-calendar-alt me-2"" aria-hidden=""true""></i>Quest Calendar</h1>").Should().BeTrue();
        Regex.IsMatch(page, "<div class=\"card-header modern-card-header calendar-head\">\\s*<h1").Should().BeTrue(
            because: "the title sits in the page card's header");

        // The two shortcuts live in the navigation bar, not in the page.
        page.Should().NotContain("Board Availability");
        page.Should().NotContain("My Agenda");
        DesktopPageRegions.NavigationBar(html).Should().Contain("Board Availability");

        var legend = Regex.Match(page, "<ul class=\"calendar-legend\" aria-label=\"Legend\">[\\s\\S]*?</ul>").Value;
        legend.Should().NotBeEmpty();
        foreach (var word in new[] { "Proposed", "Finalized", "Event", "Cancelled", "No building key", "Dashed = from a linked board" })
        {
            legend.Should().Contain($"<span>{word}</span>");
        }
        legend.Should().Contain("calendar-legend__swatch");

        var nav = Regex.Match(page, "<nav class=\"calendar-nav\"[\\s\\S]*?</nav>").Value;
        Regex.IsMatch(nav, "<a [^>]*aria-label=\"Previous month\"").Should().BeTrue();
        Regex.IsMatch(nav, "<a [^>]*aria-label=\"Next month\"").Should().BeTrue();
        nav.Should().Contain("March 2031");
        nav.Should().Contain("year=2031&amp;month=2");
        nav.Should().Contain("year=2031&amp;month=4");
        nav.Should().NotContain("btn-primary");
    }

    [Fact]
    public async Task EmptyMonth_ShowsTheOneLineNote()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await CreateViewerAsync("monthmarkup_empty", (BoardA, GroupRole.Player));

        var html = await GetAsync(client, "/Calendar?year=2033&month=1");

        var note = Regex.Match(html, "<p class=\"calendar-empty-note\"[\\s\\S]*?</p>").Value;
        note.Should().Contain("role=\"status\"");
        note.Should().Contain("fa-calendar-alt");
        note.Should().Contain("<strong>Nothing This Month</strong>");
        note.Should().Contain("No quests or events are planned for January 2033. Check another month.");
        html.IndexOf("calendar-empty-note", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("class=\"calendar-grid ", StringComparison.Ordinal),
                because: "the note sits between the header and the weekday row");
        html.Should().Contain("class=\"day-number\">31<", because: "the grid still renders its day numbers");
        html.Should().Contain("calendar-card calendar-card--empty",
            because: "an empty month tells the stylesheet to drop the slot minimum so the note never pushes the grid past the window");

        await SeedEventAsync(BoardA, "Busy Month Night", 12, new TimeOnly(18, 0));
        var busy = await GetAsync(client);
        busy.Should().NotContain("calendar-empty-note");
        busy.Should().NotContain("calendar-card--empty");
    }

    [Fact]
    public async Task EmptyMonth_CountsALinkedEntryAsContent()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        await SeedEventAsync(BoardB, "Only Linked Night", 12, new TimeOnly(19, 0));
        var (client, _) = await CreateViewerAsync("monthmarkup_emptylinked", (BoardA, GroupRole.Player));

        var html = await GetAsync(client);

        html.Should().NotContain("calendar-empty-note", because: "a month with only a linked entry is not empty");
    }

    [Fact]
    public async Task LegendPopover_HoldsEveryExistingString()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        var (client, _) = await CreateViewerAsync("monthmarkup_popover", (BoardA, GroupRole.Player));

        var page = DesktopPageRegions.PageContent(await GetAsync(client));

        var button = Regex.Match(page, "<button [^>]*dropdown-toggle[^>]*>[^<]*</button>").Value;
        button.Should().Contain("data-bs-toggle=\"dropdown\"");
        button.Should().Contain("data-bs-auto-close=\"outside\"");
        button.Should().Contain("aria-expanded=\"false\"");
        button.Should().Contain("aria-controls=\"calendar-legend-menu\"");
        button.Should().Contain("What do these mean?");

        var start = page.IndexOf("id=\"calendar-legend-menu\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var menu = page[start..page.IndexOf("</nav>", start, StringComparison.Ordinal)];
        foreach (var text in new[]
                 {
                     "Linked boards", BoardBName,
                     "Dashed entries belong to a linked board. You can open them only if you are a member of that board.",
                     "Click quests or events for details"
                 })
        {
            menu.Should().Contain(text);
        }
    }

    [Fact]
    public async Task LegendPopover_OfABoardWithNoLinkedBoards_HasNoLinkedPart()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        var (client, _) = await CreateViewerAsync("monthmarkup_solo", (BoardA, GroupRole.Player));

        var page = DesktopPageRegions.PageContent(await GetAsync(client));

        page.Should().Contain("Click quests or events for details");
        page.Should().Contain("No building key");
        page.Should().NotContain("Dashed");
        page.Should().NotContain("Linked boards");
        page.Should().NotContain("calendar-legend__swatch");
    }
}
