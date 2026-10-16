using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// A board in a linked set shows the other boards' entries on its calendar, read-only. That is
/// a deliberate step outside the ambient board filter, so the ordinary harness cannot notice a
/// leak here on its own: it never looks at a third board. These facts seed board A (the
/// seeded board), board B (linked to A) and board C (outside the set), and hold the
/// A-and-B-but-not-C case as its own fact, because absence alone cannot tell "correctly scoped
/// to the set" from "accidentally scoped to one board".
///
/// What these facts do NOT establish: isolation against the database the application runs on.
/// The shared harness backs this suite with the EF Core InMemory provider, which evaluates
/// every predicate as ordinary LINQ-to-Objects, so how the id containment test, the month
/// window and the correlated role lookup translate to SQL never reaches a relational query
/// compiler here. The facts are about the application's own scoping logic.
/// </summary>
public class LinkedBoardCalendarTenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

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
        var set = new LinkedBoardSetEntity { Name = "Isolation Set", BoardType = (int)boardType, CreatedAt = DateTime.UtcNow };
        ctx.LinkedBoardSets.Add(set);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var board in ctx.Groups.Where(g => boardIds.Contains(g.Id)).ToList())
        {
            board.LinkedBoardSetId = set.Id;
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

    private async Task SeedMembershipAsync(int userId, int groupId, GroupRole role = GroupRole.Player)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A viewer who holds exactly the memberships given and nothing else.
    private async Task<(HttpClient Client, UserEntity Viewer)> CreateViewerAsync(
        string name, params (int GroupId, GroupRole Role)[] memberships)
    {
        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, $"{name}@example.com", roles: []);
        foreach (var (groupId, role) in memberships)
        {
            await SeedMembershipAsync(viewer.Id, groupId, role);
        }
        return (client, viewer);
    }

    private async Task<string> GetCalendarAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, CalendarUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", DesktopUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The markup of one day cell, found by splitting on the day cell class and picking the
    // segment that holds that day's number.
    private static string DayCell(string html, int day)
    {
        var marker = $"<div class=\"day-number\">{day}</div>";
        return html.Split("class=\"calendar-day").Single(segment => segment.Contains(marker));
    }

    // Three boards, A and B linked, C outside. Board A's own event and one event on each of B
    // and C land on the same day, so a single request can prove or disprove each one's presence.
    private async Task<int> SeedThreeBoardsAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await SeedBoardAsync(BoardB, BoardBName);
        await SeedBoardAsync(BoardC, BoardCName);
        await SeedEventAsync(BoardA, $"Own Harbor Night {suffix}", 12, new TimeOnly(18, 0));
        var bravoEventId = await SeedEventAsync(BoardB, $"Bravo Lantern Night {suffix}", 12, new TimeOnly(19, 0));
        await SeedEventAsync(BoardC, $"Charlie Secret Night {suffix}", 12, new TimeOnly(20, 0));
        return bravoEventId;
    }

    [Fact]
    public async Task Calendar_BoardInNoSet_ShowsOnlyItsOwnEntriesAndNoLinkedMarkup()
    {
        await SeedThreeBoardsAsync("NoSet");
        var (client, _) = await CreateViewerAsync("linkedcal_noset", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        html.Should().Contain("Own Harbor Night NoSet");
        html.Should().NotContain("Bravo Lantern Night NoSet");
        html.Should().NotContain("Charlie Secret Night NoSet");
        html.Should().NotContain(BoardBName);
        html.Should().NotContain(BoardCName);
        html.Should().NotContain("linked-day-entries");
        html.Should().NotContain("linked-day-count");
        html.Should().NotContain("calendar-linked-note");
    }

    [Fact]
    public async Task Calendar_BoardAloneInItsSet_RendersNoLinkedMarkup()
    {
        await SeedThreeBoardsAsync("Alone");
        await LinkAsync(BoardType.OneShot, BoardA);
        var (client, _) = await CreateViewerAsync("linkedcal_alone", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        html.Should().Contain("Own Harbor Night Alone");
        html.Should().NotContain("Bravo Lantern Night Alone");
        html.Should().NotContain("linked-day-entries");
        html.Should().NotContain("linked-day-count");
        html.Should().NotContain("calendar-linked-note");
    }

    [Fact]
    public async Task Calendar_BoardLinkedToAnother_ShowsTheLinkedBoardsEntryAndNeverTheOutsideBoards()
    {
        await SeedThreeBoardsAsync("Linked");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_ab", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        html.Should().Contain("Own Harbor Night Linked");
        html.Should().Contain("Bravo Lantern Night Linked");
        html.Should().Contain(BoardBName);
        html.Should().NotContain("Charlie Secret Night Linked");
        html.Should().NotContain(BoardCName);
    }

    [Fact]
    public async Task Calendar_ActiveOnTheLinkedBoard_ShowsTheFirstBoardsEntryAndNotTheOutsideBoard()
    {
        await SeedThreeBoardsAsync("Reverse");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_ba", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));
        factory.TestGroupContext.ActiveGroupId = BoardB;

        var html = await GetCalendarAsync(client);

        html.Should().Contain("Bravo Lantern Night Reverse");
        html.Should().Contain("Own Harbor Night Reverse");
        html.Should().NotContain("Charlie Secret Night Reverse");
        html.Should().NotContain(BoardCName);
    }

    [Fact]
    public async Task Calendar_AfterABoardIsRemovedFromTheSet_NextRequestShowsNothingFromIt()
    {
        await SeedThreeBoardsAsync("Removed");
        var setId = await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_removed", (BoardA, GroupRole.Player));

        var before = await GetCalendarAsync(client);
        before.Should().Contain("Bravo Lantern Night Removed");

        using (var scope = factory.Services.CreateScope())
        {
            var setService = scope.ServiceProvider.GetRequiredService<ILinkedBoardSetService>();
            await setService.RemoveBoardAsync(setId, BoardB, TestContext.Current.CancellationToken);
        }

        var after = await GetCalendarAsync(client);
        after.Should().Contain("Own Harbor Night Removed");
        after.Should().NotContain("Bravo Lantern Night Removed");
        after.Should().NotContain(BoardBName);
        after.Should().NotContain("linked-day-entries");
    }

    [Fact]
    public async Task Calendar_MemberOfTheLinkedBoard_GetsOneAnchorToItsOrdinaryDetailsPage()
    {
        var bravoEventId = await SeedThreeBoardsAsync("Member");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_member", (BoardA, GroupRole.Player), (BoardB, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        html.Should().Contain($"href=\"/Events/Details/{bravoEventId}\"");
        var cell = DayCell(html, 12);
        cell.Should().Contain("linked-entry--open");
        cell.Should().NotContain("linked-entry--static");
    }

    [Fact]
    public async Task Calendar_NonMemberOfTheLinkedBoard_SeesTheEntryAsPlainTextWithNoLink()
    {
        var bravoEventId = await SeedThreeBoardsAsync("NonMember");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_nonmember", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        html.Should().Contain("Bravo Lantern Night NonMember");
        html.Should().Contain(BoardBName);
        html.Should().Contain("19:00");
        html.Should().NotContain($"/Events/Details/{bravoEventId}");
        var cell = DayCell(html, 12);
        cell.Should().Contain("linked-entry--static");
        cell.Should().NotContain("linked-entry--open");
    }

    [Fact]
    public async Task Calendar_NonMemberChip_CarriesNoLinkTabStopRoleOrDataAttributes()
    {
        await SeedThreeBoardsAsync("Plain");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_plain", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        var linkedBlock = LinkedBlock(DayCell(html, 12));
        linkedBlock.Should().NotContain("href=");
        linkedBlock.Should().NotContain("onclick");
        linkedBlock.Should().NotContain("tabindex");
        linkedBlock.Should().NotContain("role=");
        linkedBlock.Should().NotContain("data-");
        linkedBlock.Should().NotContain("<form");
        linkedBlock.Should().NotContain("<input");
    }

    [Fact]
    public async Task Calendar_CancelledLinkedEvent_IsShownAndMarkedCancelled()
    {
        await SeedThreeBoardsAsync("Cancelled");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        await SeedEventAsync(BoardB, "Bravo Called Off Night Cancelled", 20, new TimeOnly(17, 0), cancelled: true);
        var (client, _) = await CreateViewerAsync("linkedcal_cancelled", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        var cell = DayCell(html, 20);
        cell.Should().Contain("Bravo Called Off Night Cancelled");
        cell.Should().Contain("linked-entry--cancelled");
        cell.Should().Contain("Cancelled.");
        DayCell(html, 12).Should().NotContain("linked-entry--cancelled");
    }

    [Fact]
    public async Task Calendar_DayWithOwnAndLinkedEntries_ShowsOwnBeforeLinked()
    {
        await SeedThreeBoardsAsync("Order");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        var (client, _) = await CreateViewerAsync("linkedcal_order", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        var cell = DayCell(html, 12);
        var ownAt = cell.IndexOf("Own Harbor Night Order", StringComparison.Ordinal);
        var linkedAt = cell.IndexOf("Bravo Lantern Night Order", StringComparison.Ordinal);
        ownAt.Should().BeGreaterThan(-1);
        linkedAt.Should().BeGreaterThan(ownAt);
    }

    [Fact]
    public async Task Calendar_DayWithOnlyALinkedEntry_ShowsTheChipAndTheCountBadge()
    {
        await SeedThreeBoardsAsync("OnlyLinked");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        await SeedEventAsync(BoardB, "Bravo Lonely Night OnlyLinked", 25, new TimeOnly(16, 0));
        var (client, _) = await CreateViewerAsync("linkedcal_onlylinked", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        var cell = DayCell(html, 25);
        cell.Should().Contain("Bravo Lonely Night OnlyLinked");
        cell.Should().Contain("linked-day-count");
        cell.Should().Contain("1 entries from linked boards");
        DayCell(html, 12).Should().Contain("1 entries from linked boards");
        DayCell(html, 3).Should().NotContain("linked-day-count");
    }

    [Fact]
    public async Task Calendar_LinkedEventOutsideTheShownMonth_IsNotShown()
    {
        await SeedThreeBoardsAsync("Window");
        await LinkAsync(BoardType.OneShot, BoardA, BoardB);
        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Events.Add(new EventEntity
            {
                Title = "Bravo Other Month Night Window",
                GroupId = BoardB,
                Date = new DateOnly(Year, Month + 1, 2),
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var (client, _) = await CreateViewerAsync("linkedcal_window", (BoardA, GroupRole.Player));

        var html = await GetCalendarAsync(client);

        html.Should().Contain("Bravo Lantern Night Window");
        html.Should().NotContain("Bravo Other Month Night Window");
    }

    // The linked entries block of one day cell, from its wrapper to the end of the cell.
    private static string LinkedBlock(string cell)
    {
        var start = cell.IndexOf("linked-day-entries", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        return cell[start..];
    }
}
