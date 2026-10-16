using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The phone agenda lists the entries of the other boards in the active board's linked set after
/// the board's own, read-only, and names those boards under the month bar. The phone view builds
/// its own list rather than sharing the desktop partial, so it needs its own proof. Seeding is
/// kept in this class on purpose: board A is the seeded board, board B is linked to A, board C
/// is outside the set.
///
/// What these facts do NOT establish: how the linked reads translate to SQL. The shared harness
/// uses the EF Core InMemory provider, so only the application's own scoping logic is exercised.
/// </summary>
public class LinkedBoardMobileCalendarTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardA = 1;
    private const int BoardB = 2;
    private const int BoardC = 3;
    private const string BoardBName = "Linked Board Bravo";
    private const string BoardCName = "Outside Board Charlie";

    // A fixed month far from the present, so no fact depends on what day it is when it runs.
    private const int Year = 2031;
    private const int Month = 3;
    private const string CalendarUrl = "/Calendar?year=2031&month=3";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static DateOnly DayOfMonth(int day) => new(Year, Month, day);

    private async Task SeedBoardAsync(int groupId, string name, BoardType boardType = BoardType.OneShot)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == groupId))
        {
            ctx.Groups.Add(new GroupEntity { Id = groupId, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)boardType });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // Puts the named boards into one new set by assigning the column on tracked board rows.
    private async Task<int> LinkAsync(BoardType boardType, params int[] boardIds)
    {
        await using var ctx = factory.Database.CreateContext();
        var set = new LinkedBoardSetEntity { Name = "Mobile Set", BoardType = (int)boardType, CreatedAt = DateTime.UtcNow };
        ctx.LinkedBoardSets.Add(set);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var board in ctx.Groups.Where(g => boardIds.Contains(g.Id)).ToList())
        {
            board.LinkedBoardSetId = set.Id;
            board.BoardType = (int)boardType;
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return set.Id;
    }

    private async Task<int> SeedEventAsync(
        int groupId, string title, int day, TimeOnly? startTime = null, bool cancelled = false)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = DayOfMonth(day),
            StartTime = startTime,
            CancelledAt = cancelled ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // Seeds one quest on the named board with the given proposed days (19:00 each). A finalized
    // quest is finalized on the first proposed day.
    private async Task<int> SeedQuestAsync(
        int groupId, string title, string dmName, int[] proposedDays, bool finalized = false, bool dmOnly = false)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mobilecal_dm", "mobilecal_dm@example.com", name: dmName);

        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Mobile linked calendar test quest",
            ChallengeRating = 5,
            DungeonMasterId = dm.Id,
            DungeonMasterSession = dmOnly,
            TotalPlayerCount = 4,
            GroupId = groupId,
            IsFinalized = finalized,
            FinalizedDate = finalized ? new DateTime(Year, Month, proposedDays[0], 19, 0, 0) : null,
            CreatedAt = DateTime.UtcNow,
            ProposedDates = [.. proposedDays.Select(d => new ProposedDateEntity { Date = new DateTime(Year, Month, d, 19, 0, 0) })]
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    private async Task SeedMembershipAsync(int userId, int groupId, GroupRole role)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A viewer who holds exactly the memberships given and nothing else.
    private async Task<HttpClient> CreateViewerAsync(string name, params (int GroupId, GroupRole Role)[] memberships)
    {
        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, $"{name}@example.com", roles: []);
        foreach (var (groupId, role) in memberships)
        {
            await SeedMembershipAsync(viewer.Id, groupId, role);
        }
        return client;
    }

    private static async Task<string> GetAgendaAsync(HttpClient client, string url = CalendarUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string DayLabel(int day) =>
        new DateTime(Year, Month, day).ToString("dddd, MMMM d").ToUpper();

    // The markup of one agenda day section, found by splitting on the section class and
    // picking the segment whose label is that day. Null when the day has no section.
    private static string? DaySection(string html, int day)
    {
        var label = DayLabel(day);
        return html.Split("class=\"agenda-day-section")
            .Skip(1)
            .SingleOrDefault(segment => segment.Contains(label));
    }

    // Two linked boards plus an outside board. Board A's own event and one event on each of B
    // and C land on day 12, so one request proves or disproves each one's presence.
    private async Task SeedThreeBoardsAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await SeedBoardAsync(BoardB, BoardBName);
        await SeedBoardAsync(BoardC, BoardCName);
        await SeedEventAsync(BoardA, $"Own Harbor Night {suffix}", 12, new TimeOnly(18, 0));
        await SeedEventAsync(BoardB, $"Bravo Lantern Night {suffix}", 12, new TimeOnly(19, 0));
        await SeedEventAsync(BoardC, $"Charlie Secret Night {suffix}", 12, new TimeOnly(20, 0));
    }

    private async Task SeedLinkedPairAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await SeedBoardAsync(BoardB, BoardBName);
        await SeedBoardAsync(BoardC, BoardCName);
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
    }

    [Fact]
    public async Task Agenda_LinkedBoardsEventAndOpenQuest_AreListedAndTheOutsideBoardsAreNot()
    {
        await SeedThreeBoardsAsync("Listed");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        await SeedQuestAsync(BoardB, "Bravo Open Crypt Run Listed", "Dungeon Master Dana", [10]);
        await SeedQuestAsync(BoardC, "Charlie Open Crypt Run Listed", "Dungeon Master Dana", [10]);
        var client = await CreateViewerAsync("mobilecal_listed", (BoardA, GroupRole.Player));

        var html = await GetAgendaAsync(client);

        html.Should().Contain("Own Harbor Night Listed");
        html.Should().Contain("Bravo Lantern Night Listed");
        html.Should().Contain("Bravo Open Crypt Run Listed");
        html.Should().Contain(BoardBName);
        html.Should().NotContain("Charlie Secret Night Listed");
        html.Should().NotContain("Charlie Open Crypt Run Listed");
        html.Should().NotContain(BoardCName);
    }

    [Fact]
    public async Task Agenda_DayOnlyTheLinkedBoardUses_GetsItsOwnDaySectionWithTheStandardLabel()
    {
        await SeedThreeBoardsAsync("OnlyLinked");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        await SeedEventAsync(BoardB, "Bravo Lonely Night OnlyLinked", 25, new TimeOnly(16, 0));
        var client = await CreateViewerAsync("mobilecal_onlylinked", (BoardA, GroupRole.Player));

        var html = await GetAgendaAsync(client);

        var section = DaySection(html, 25);
        section.Should().NotBeNull();
        section.Should().Contain("Bravo Lonely Night OnlyLinked");
        section.Should().Contain("16:00");
    }

    [Fact]
    public async Task Agenda_DayWithOwnAndLinkedEntries_ListsOwnBeforeLinked()
    {
        await SeedThreeBoardsAsync("Order");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        await SeedQuestAsync(BoardA, "Own Quest Run Order", "Dungeon Master Dana", [12]);
        await SeedQuestAsync(BoardB, "Bravo Quest Run Order", "Dungeon Master Dana", [12]);
        var client = await CreateViewerAsync("mobilecal_order", (BoardA, GroupRole.Player));

        var html = await GetAgendaAsync(client);

        var section = DaySection(html, 12)!;
        var ownEvent = section.IndexOf("Own Harbor Night Order", StringComparison.Ordinal);
        var ownQuest = section.IndexOf("Own Quest Run Order", StringComparison.Ordinal);
        var linkedEvent = section.IndexOf("Bravo Lantern Night Order", StringComparison.Ordinal);
        var linkedQuest = section.IndexOf("Bravo Quest Run Order", StringComparison.Ordinal);
        ownEvent.Should().BeGreaterThan(-1);
        ownQuest.Should().BeGreaterThan(ownEvent);
        linkedEvent.Should().BeGreaterThan(ownQuest);
        linkedQuest.Should().BeGreaterThan(linkedEvent);
    }

    [Fact]
    public async Task Agenda_BoardWithLinkedBoards_SaysWhichBoardsItIsShowing()
    {
        await SeedThreeBoardsAsync("Note");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var client = await CreateViewerAsync("mobilecal_note", (BoardA, GroupRole.Player));

        var html = await GetAgendaAsync(client);

        html.Should().Contain("Also showing: Linked Board Bravo");
        html.IndexOf("agenda-linked-note", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("calendar-month-nav", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Agenda_BoardInNoSet_RendersNoLinkedMarkupAtAll()
    {
        await SeedThreeBoardsAsync("NoSet");
        var client = await CreateViewerAsync("mobilecal_noset", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));

        var html = await GetAgendaAsync(client);

        html.Should().Contain("Own Harbor Night NoSet");
        html.Should().NotContain("Bravo Lantern Night NoSet");
        html.Should().NotContain("agenda-linked");
        html.Should().NotContain("Also showing");
    }

    [Fact]
    public async Task Agenda_BoardAloneInItsSet_HasNoAlsoShowingLine()
    {
        await SeedThreeBoardsAsync("Alone");
        await LinkAsync(BoardType.OneShot, BoardA);
        var client = await CreateViewerAsync("mobilecal_alone", (BoardA, GroupRole.Player));

        var html = await GetAgendaAsync(client);

        html.Should().Contain("Own Harbor Night Alone");
        html.Should().NotContain("Also showing");
        html.Should().NotContain("agenda-linked");
    }

    [Fact]
    public async Task Agenda_MonthWithNoEntries_KeepsTheNothingThisMonthState()
    {
        await SeedThreeBoardsAsync("Empty");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var client = await CreateViewerAsync("mobilecal_empty", (BoardA, GroupRole.Player));

        var html = await GetAgendaAsync(client, "/Calendar?year=2031&month=8");

        html.Should().Contain("Nothing This Month");
        html.Should().NotContain("agenda-linked-entry");
    }
}
