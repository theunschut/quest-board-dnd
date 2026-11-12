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
/// The phone Calendar page: its title and month buttons, the legend behind its toggle, the flags an
/// own quest slip carries, the board's today marker, the empty month and the slips of linked boards.
/// Every request is made under a phone User-Agent so the phone layout is the one rendered.
/// </summary>
public class CalendarPhoneAgendaTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardA = 1;
    private const int BoardB = 2;
    private const string BoardBName = "Phone Linked Bravo";

    // A fixed month far from the present, so no fact depends on what day it is when it runs.
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

    private static async Task<string> GetPhoneAsync(HttpClient client, string url = CalendarUrl)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string DayLabel(int day) =>
        new DateTime(Year, Month, day).ToString("dddd, MMMM d");

    // The markup of one agenda day section, found by the label of its day. Null when the day has none.
    private static string? DaySection(string html, int day) =>
        html.Split("<section class=\"agenda-day-section", StringSplitOptions.None)
            .Skip(1)
            .Select(segment => segment[..segment.IndexOf("</section>", StringComparison.Ordinal)])
            .SingleOrDefault(segment => segment.Contains(DayLabel(day), StringComparison.OrdinalIgnoreCase));

    private async Task<int> SeedQuestAsync(
        int groupId,
        string title,
        string dmName,
        int[] proposedDays,
        bool finalized = false,
        bool dmHasKey = false,
        Func<List<ProposedDateEntity>, List<PlayerSignupEntity>>? signups = null)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "phoneagenda_dm", "phoneagenda_dm@example.com", name: dmName);

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
            Description = "Phone agenda test quest",
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

    private async Task<int> SeedEventAsync(int groupId, string title, int day, TimeOnly? startTime = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var entity = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = new DateOnly(Year, Month, day),
            StartTime = startTime,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity.Id;
    }

    private async Task SeedLinkedPairAsync()
    {
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await BoardMoveSeed.SeedBoardAsync(factory, BoardB, BoardBName, BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardA, BoardB);
    }

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

    [Fact]
    public async Task Header_HasTheTitleAndNamedMonthButtons()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync(BoardA, "Phone Header Night", 12, new TimeOnly(18, 0));
        var (client, _) = await CreateViewerAsync("phoneagenda_header", (BoardA, GroupRole.Player));

        var html = await GetPhoneAsync(client);

        var page = PhonePageRegions.PageContent(html);
        Regex.Matches(page, "<h1[ >]").Should().HaveCount(1, because: "the page has one title");
        Regex.IsMatch(page, @"<h1[^>]*>[\s\S]*?Quest Calendar\s*</h1>").Should().BeTrue();
        Regex.IsMatch(page, "<div class=\"card-header modern-card-header\">\\s*<h1").Should().BeTrue(
            because: "the title sits in the page card's header");

        var nav = Regex.Match(page, "<nav class=\"agenda-month-nav\"[\\s\\S]*?</nav>").Value;
        nav.Should().NotBeEmpty();
        Regex.IsMatch(nav, "<a [^>]*aria-label=\"Previous month\"").Should().BeTrue();
        Regex.IsMatch(nav, "<a [^>]*aria-label=\"Next month\"").Should().BeTrue();
        nav.Should().Contain("March 2031");
        nav.Should().Contain("year=2031&amp;month=2");
        nav.Should().Contain("year=2031&amp;month=4");
    }

    [Fact]
    public async Task Legend_IsBehindTheToggleWithEveryString()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        await SeedEventAsync(BoardA, "Phone Legend Night", 12, new TimeOnly(18, 0));
        var (client, _) = await CreateViewerAsync("phoneagenda_legend", (BoardA, GroupRole.Player));

        var html = await GetPhoneAsync(client);

        var toggle = Regex.Match(html, "<button [^>]*calendar-legend-toggle[^>]*>[^<]*</button>").Value;
        toggle.Should().Contain("data-bs-toggle=\"collapse\"");
        toggle.Should().Contain("data-bs-target=\"#agenda-legend\"");
        toggle.Should().Contain("aria-controls=\"agenda-legend\"");
        toggle.Should().Contain("aria-expanded=\"false\"");
        toggle.Should().Contain("What do these mean?");

        var start = html.IndexOf("id=\"agenda-legend\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var tag = html[html.LastIndexOf("<div", start, StringComparison.Ordinal)..html.IndexOf('>', start)];
        tag.Should().Contain("class=\"collapse\"", because: "the legend stays closed until asked for");

        var legend = html[start..html.IndexOf("class=\"agenda-days\"", start, StringComparison.Ordinal)];
        foreach (var text in new[]
                 {
                     "Proposed", "Finalized", "Event", "Cancelled", "No building key", "Dashed = from a linked board",
                     "Linked boards", BoardBName,
                     "Dashed entries belong to a linked board. You can open them only if you are a member of that board.",
                     "Click quests or events for details"
                 })
        {
            legend.Should().Contain(text);
        }
    }

    [Fact]
    public async Task Legend_OfABoardWithNoLinkedBoards_HasNoLinkedPart()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await SeedEventAsync(BoardA, "Phone Solo Night", 12, new TimeOnly(18, 0));
        var (client, _) = await CreateViewerAsync("phoneagenda_solo", (BoardA, GroupRole.Player));

        var html = await GetPhoneAsync(client);

        var start = html.IndexOf("id=\"agenda-legend\"", StringComparison.Ordinal);
        var legend = html[start..html.IndexOf("class=\"agenda-days\"", start, StringComparison.Ordinal)];
        legend.Should().Contain("No building key");
        legend.Should().Contain("Click quests or events for details");
        legend.Should().NotContain("Dashed");
        legend.Should().NotContain("Linked boards");
    }

    [Fact]
    public async Task OwnQuestRows_ShowVoteKeyAndFinalizedFlagsAndNoDmName()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, viewer) = await CreateViewerAsync("phoneagenda_votes", (BoardA, GroupRole.Player));
        var other = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "phoneagenda_other", "phoneagenda_other@example.com", name: "Other Voter");

        // Day 10: viewer voted Maybe, no key held by anyone. Day 12: the viewer voted Yes on a date and
        // the DM holds a key. Day 14: finalized and the DM holds a key. Day 16: only someone else voted.
        await SeedQuestAsync(BoardA, "Phone Maybe Quest", "Dungeon Master Dana", [10],
            signups: dates => [new PlayerSignupEntity
            {
                PlayerId = viewer.Id,
                DateVotes = [new PlayerDateVoteEntity { ProposedDate = dates[0], Vote = (int)VoteType.Maybe }]
            }]);
        await SeedQuestAsync(BoardA, "Phone Yes Quest", "Dungeon Master Dana", [12], dmHasKey: true,
            signups: dates => [new PlayerSignupEntity
            {
                PlayerId = viewer.Id,
                DateVotes = [new PlayerDateVoteEntity { ProposedDate = dates[0], Vote = (int)VoteType.Yes }]
            }]);
        await SeedQuestAsync(BoardA, "Phone Final Quest", "Dungeon Master Dana", [14], finalized: true, dmHasKey: true);
        await SeedQuestAsync(BoardA, "Phone Stranger Quest", "Dungeon Master Dana", [16], dmHasKey: true,
            signups: dates => [new PlayerSignupEntity
            {
                PlayerId = other.Id,
                DateVotes = [new PlayerDateVoteEntity { ProposedDate = dates[0], Vote = (int)VoteType.No }]
            }]);

        var html = await GetPhoneAsync(client);

        var maybe = DaySection(html, 10)!;
        maybe.Should().Contain("Your vote: Maybe");
        maybe.Should().Contain("agenda-flag--vote-maybe");
        maybe.Should().Contain("No building key");
        maybe.Should().Contain("<span class=\"visually-hidden\">Proposed.</span>");

        var yes = DaySection(html, 12)!;
        yes.Should().Contain("Your vote: Yes");
        yes.Should().NotContain("No building key");

        var final = DaySection(html, 14)!;
        final.Should().Contain("agenda-quest-finalized");
        final.Should().Contain("<span class=\"visually-hidden\">Finalized.</span>");
        final.Should().Contain("fa-circle-check");
        final.Should().NotContain("Your vote");
        final.Should().NotContain("No building key");

        var stranger = DaySection(html, 16)!;
        stranger.Should().NotContain("Your vote", because: "another player's vote is never shown as the viewer's");

        html.Should().NotContain("Dungeon Master Dana", because: "an own quest slip never names its Dungeon Master");
        PhonePageRegions.PageContent(html).Should().NotContain("onclick");
    }

    [Fact]
    public async Task OwnQuestRows_FlagsReadTheirWordsAndHideTheirIcons()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, viewer) = await CreateViewerAsync("phoneagenda_words", (BoardA, GroupRole.Player));
        await SeedQuestAsync(BoardA, "Phone Words Quest", "Dungeon Master Dana", [10],
            signups: dates => [new PlayerSignupEntity
            {
                PlayerId = viewer.Id,
                DateVotes = [new PlayerDateVoteEntity { ProposedDate = dates[0], Vote = (int)VoteType.No }]
            }]);

        var html = await GetPhoneAsync(client);

        var section = DaySection(html, 10)!;
        section.Should().Contain("<i class=\"fas fa-times\" aria-hidden=\"true\"></i>Your vote: No");
        section.Should().Contain("<i class=\"fas fa-exclamation-triangle\" aria-hidden=\"true\"></i>No building key");
    }

    [Fact]
    public async Task TodaySection_ShowsThePillOnlyForTheBoardsToday()
    {
        // At this instant the board (Europe/Amsterdam) has turned over to the 6th while the container
        // clock is still on the 5th.
        var pinned = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["TimeZone:BoardTimeZoneId"] = "Europe/Amsterdam" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 23, 30, 0, TimeSpan.Zero)));
            });
        });

        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            pinned, "phoneagenda_today", "phoneagenda_today@example.com");
        await BoardMoveSeed.SeedEventAsync(factory, 1, "Phone Fifth", new DateOnly(2026, 9, 5));
        await BoardMoveSeed.SeedEventAsync(factory, 1, "Phone Sixth", new DateOnly(2026, 9, 6));

        var html = await GetPhoneAsync(client, "/Calendar?year=2026&month=9");

        var sections = html.Split("<section class=\"agenda-day-section", StringSplitOptions.None).Skip(1).ToArray();
        var sixth = sections.Single(s => Regex.IsMatch(s, "september\\s+6\\b", RegexOptions.IgnoreCase));
        sixth.Should().Contain("aria-current=\"date\"");
        sixth.Should().Contain("agenda-day-section--today");
        Regex.IsMatch(sixth, "<span class=\"agenda-today-pill\">Today</span>").Should().BeTrue();

        var fifth = sections.Single(s => Regex.IsMatch(s, "september\\s+5\\b", RegexOptions.IgnoreCase));
        fifth.Should().NotContain("aria-current");
        fifth.Should().NotContain("agenda-today-pill");
        fifth.Should().NotContain("agenda-day-section--today");

        Regex.Matches(html, "agenda-today-pill\">Today<").Should().HaveCount(1);
    }

    [Fact]
    public async Task EmptyMonth_UsesTheEmptyStateVerbatim()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await CreateViewerAsync("phoneagenda_empty", (BoardA, GroupRole.Player));

        var html = await GetPhoneAsync(client, "/Calendar?year=2031&month=8");

        var page = PhonePageRegions.PageContent(html);
        var empty = Regex.Match(page, "<div class=\"empty-state\">[\\s\\S]*?</div>").Value;
        empty.Should().NotBeEmpty();
        empty.Should().Contain("fa-calendar-alt");
        empty.Should().Contain("aria-hidden=\"true\"");
        empty.Should().Contain("<h2 class=\"empty-state-heading\">Nothing This Month</h2>");
        empty.Should().Contain("No quests or events are planned for August 2031. Check another month.");
        page.Should().NotContain("agenda-day-section");
    }

    [Fact]
    public async Task LinkedRows_MemberLinksAndInertStays()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedLinkedPairAsync();
        var openQuest = await SeedQuestAsync(BoardB, "Phone Open Linked Quest", "Dungeon Master Dana", [10]);
        await SeedQuestAsync(BoardB, "Phone Inert Linked Quest", "Dungeon Master Dana", [14]);
        var (memberClient, _) = await CreateViewerAsync(
            "phoneagenda_member", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));
        var (outsiderClient, _) = await CreateViewerAsync("phoneagenda_outsider", (BoardA, GroupRole.Player));

        var memberHtml = await GetPhoneAsync(memberClient);
        var outsiderHtml = await GetPhoneAsync(outsiderClient);

        var open = DaySection(memberHtml, 10)!;
        open.Should().Contain("agenda-linked-entry--open");
        Regex.Matches(open, "<a ").Should().HaveCount(1);
        open.Should().Contain($"href=\"/Quest/Details/{openQuest}\"");
        open.Should().Contain(BoardBName);
        open.Should().Contain("Dungeon Master Dana");
        open.Should().NotContain("onclick");

        // The day label is a time element with data attributes of its own, so the slip is cut out first.
        var daySection = DaySection(outsiderHtml, 14)!;
        var inert = daySection[daySection.IndexOf("<li ", StringComparison.Ordinal)..];
        inert.Should().Contain("agenda-linked-entry--static");
        inert.Should().NotContain("<a ");
        inert.Should().NotContain("tabindex");
        inert.Should().NotContain("data-");
        inert.Should().NotContain("onclick");
        inert.Should().Contain("<span class=\"visually-hidden\">Proposed.</span>");
        inert.Should().Contain("Phone Inert Linked Quest");
        inert.Should().Contain(BoardBName);

        // The same slip for a member is a link; the difference is the server's decision, nothing else.
        DaySection(memberHtml, 14)!.Should().Contain("<a class=\"row-nav-link\"");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
