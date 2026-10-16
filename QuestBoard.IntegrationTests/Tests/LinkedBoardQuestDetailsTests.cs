using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The Quest Details date calendar is where a clash between boards is actually decided, so a
/// board in a linked set shows the other boards' sessions there, read-only. The page's date
/// markup sits inside the form that posts the votes, so these facts also hold that linking
/// never adds a vote input and that the linked markup carries no form control at all.
///
/// Seeded: board A (the seeded board, active), board B (linked to A) and board C (outside the
/// set). Quest X on A proposes the 10th and 11th of a fixed future month; A's own event sits on
/// the 10th. B has a quest on the 10th and an event on the 12th, C has a quest on the 10th.
///
/// What these facts do NOT establish: how the linked reads translate to SQL. The shared harness
/// uses the EF Core InMemory provider.
/// </summary>
public class LinkedBoardQuestDetailsTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

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

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task SeedBoardAsync(int groupId, string name)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == groupId))
        {
            ctx.Groups.Add(new GroupEntity { Id = groupId, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)BoardType.OneShot });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<int> LinkAsync(params int[] boardIds)
    {
        await using var ctx = factory.Database.CreateContext();
        var set = new LinkedBoardSetEntity { Name = "Details Set", BoardType = (int)BoardType.OneShot, CreatedAt = DateTime.UtcNow };
        ctx.LinkedBoardSets.Add(set);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var board in ctx.Groups.Where(g => boardIds.Contains(g.Id)).ToList())
        {
            board.LinkedBoardSetId = set.Id;
            board.BoardType = (int)BoardType.OneShot;
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return set.Id;
    }

    private async Task SeedMembershipAsync(int userId, int groupId, GroupRole role)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> SeedEventAsync(
        int groupId, string title, int day, TimeOnly? startTime = null, bool cancelled = false)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = new DateOnly(Year, Month, day),
            StartTime = startTime,
            CancelledAt = cancelled ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // One open quest on the named board, with proposed days at 19:00, owned by the given user
    // or by a fresh user named after the DM.
    private async Task<int> SeedQuestAsync(
        int groupId, string title, int[] proposedDays, int? dmUserId = null,
        string dmName = "Dungeon Master Dana", bool dmOnly = false)
    {
        var dmId = dmUserId
            ?? (await AuthenticationHelper.CreateTestUserAsync(
                factory.Services, "detailslink_dm", "detailslink_dm@example.com", name: dmName)).Id;

        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Linked details test quest",
            ChallengeRating = 5,
            DungeonMasterId = dmId,
            DungeonMasterSession = dmOnly,
            TotalPlayerCount = 4,
            GroupId = groupId,
            IsFinalized = false,
            CreatedAt = DateTime.UtcNow,
            ProposedDates = [.. proposedDays.Select(d => new ProposedDateEntity { Date = new DateTime(Year, Month, d, 19, 0, 0) })]
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    private record Scenario(int QuestXId, int BravoQuestId, int BravoEventId, HttpClient DmClient, HttpClient PlayerClient);

    // Boards A, B and C; A and B linked. Quest X belongs to the DM client's user.
    private async Task<Scenario> SeedScenarioAsync(bool link = true)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await SeedBoardAsync(BoardB, BoardBName);
        await SeedBoardAsync(BoardC, BoardCName);
        if (link)
        {
            await LinkAsync(BoardA, BoardB);
        }

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailslink_xdm", "detailslink_xdm@example.com", roles: ["DungeonMaster"]);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailslink_player", "detailslink_player@example.com", roles: ["Player"]);

        var questXId = await SeedQuestAsync(BoardA, "Alpha Proposed Quest X", [10, 11], dmUser.Id);
        await SeedEventAsync(BoardA, "Alpha Own Harbor Night", 10, new TimeOnly(18, 0));
        var bravoQuestId = await SeedQuestAsync(BoardB, "Bravo Linked Crypt Run", [10]);
        var bravoEventId = await SeedEventAsync(BoardB, "Bravo Linked Lantern Night", 12, new TimeOnly(20, 0));
        await SeedQuestAsync(BoardC, "Charlie Outside Quest", [10]);

        return new Scenario(questXId, bravoQuestId, bravoEventId, dmClient, playerClient);
    }

    private async Task<string> GetDetailsAsync(HttpClient client, int questId, string userAgent = DesktopUserAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{questId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // Every linked-day-entries block on the page, each cut at its own balanced closing tag.
    private static List<string> LinkedBlocks(string html)
    {
        var blocks = new List<string>();
        var from = 0;
        while (true)
        {
            var marker = html.IndexOf("class=\"linked-day-entries\"", from, StringComparison.Ordinal);
            if (marker < 0)
            {
                return blocks;
            }

            var start = html.LastIndexOf("<div", marker, StringComparison.Ordinal);
            var depth = 0;
            var end = html.Length;
            foreach (Match tag in Regex.Matches(html[start..], @"<div\b|</div>"))
            {
                depth += tag.Value == "</div>" ? -1 : 1;
                if (depth == 0)
                {
                    end = start + tag.Index + tag.Length;
                    break;
                }
            }

            blocks.Add(html[start..end]);
            from = end;
        }
    }

    private static int CountOf(string html, string needle) =>
        Regex.Matches(html, Regex.Escape(needle)).Count;

    [Fact]
    public async Task Details_Desktop_ShowsTheLinkedBoardsQuestAndEventInsideTheLinkedBlocks()
    {
        var scenario = await SeedScenarioAsync();

        foreach (var client in new[] { scenario.DmClient, scenario.PlayerClient })
        {
            var html = await GetDetailsAsync(client, scenario.QuestXId);

            html.Should().Contain("Alpha Proposed Quest X");
            var linked = string.Concat(LinkedBlocks(html));
            linked.Should().Contain("Bravo Linked Crypt Run");
            linked.Should().Contain("Bravo Linked Lantern Night");
            html.Should().NotContain("Charlie Outside Quest");
            html.Should().NotContain(BoardCName);
        }
    }

    [Fact]
    public async Task Details_Desktop_KeepsTheBoardsOwnEventOffThePageWhileLinkedEventsShow()
    {
        var scenario = await SeedScenarioAsync();

        var html = await GetDetailsAsync(scenario.DmClient, scenario.QuestXId);

        html.Should().Contain("Bravo Linked Lantern Night");
        html.Should().NotContain("Alpha Own Harbor Night");
        html.Should().NotContain("calendar-events");
        html.Should().NotContain("calendar-event");
    }

    [Fact]
    public async Task Details_Desktop_ShowsTheReadOnlyNoteNamingTheLinkedBoard()
    {
        var scenario = await SeedScenarioAsync();

        var html = await GetDetailsAsync(scenario.DmClient, scenario.QuestXId);

        html.Should().Contain("calendar-linked-note");
        html.Should().Contain($"Also showing: {BoardBName} (read-only)");
    }

    [Fact]
    public async Task Details_Desktop_LinkedBlocksCarryNoFormControlOrVoteName()
    {
        var scenario = await SeedScenarioAsync();

        var html = await GetDetailsAsync(scenario.PlayerClient, scenario.QuestXId);

        var blocks = LinkedBlocks(html);
        blocks.Should().NotBeEmpty();
        foreach (var block in blocks)
        {
            block.Should().NotContain("<input");
            block.Should().NotContain("<label");
            block.Should().NotContain("<button");
            block.Should().NotContain("<form");
            block.Should().NotContain("DateVotes");
        }
    }

    [Fact]
    public async Task Details_Desktop_LinkingAddsNoVoteInputToThePlayersForm()
    {
        var scenario = await SeedScenarioAsync();
        var setId = await GetSetIdAsync();

        var linkedHtml = await GetDetailsAsync(scenario.PlayerClient, scenario.QuestXId);
        var linkedCount = CountOf(linkedHtml, "name=\"DateVotes[");
        linkedCount.Should().BeGreaterThan(0);
        linkedHtml.Should().Contain("Bravo Linked Crypt Run");

        using (var scope = factory.Services.CreateScope())
        {
            var setService = scope.ServiceProvider.GetRequiredService<ILinkedBoardSetService>();
            await setService.RemoveBoardAsync(setId, BoardB, TestContext.Current.CancellationToken);
        }

        var unlinkedHtml = await GetDetailsAsync(scenario.PlayerClient, scenario.QuestXId);
        unlinkedHtml.Should().NotContain("Bravo Linked Crypt Run");
        CountOf(unlinkedHtml, "name=\"DateVotes[").Should().Be(linkedCount);
    }

    private async Task<int> GetSetIdAsync()
    {
        await using var ctx = factory.Database.CreateContext();
        return ctx.LinkedBoardSets.Select(s => s.Id).Single();
    }

    [Fact]
    public async Task Details_Desktop_BoardInNoSet_RendersNoLinkedMarkup()
    {
        var scenario = await SeedScenarioAsync(link: false);

        foreach (var client in new[] { scenario.DmClient, scenario.PlayerClient })
        {
            var html = await GetDetailsAsync(client, scenario.QuestXId);

            html.Should().Contain("Alpha Proposed Quest X");
            html.Should().NotContain("calendar-linked-note");
            html.Should().NotContain("linked-day-entries");
            html.Should().NotContain("linked-day-count");
            html.Should().NotContain("Bravo Linked Crypt Run");
            html.Should().NotContain("Bravo Linked Lantern Night");
        }
    }

    [Fact]
    public async Task Details_Desktop_LinkedDmOnlySessionIsHiddenFromAPlayerAndShownToADungeonMasterOfTheOwningBoard()
    {
        var scenario = await SeedScenarioAsync();
        await SeedQuestAsync(BoardB, "Bravo Secret Session Details", [10], dmOnly: true);
        var (dmOnBClient, dmOnB) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailslink_dmonb", "detailslink_dmonb@example.com", roles: ["Player"]);
        await SeedMembershipAsync(dmOnB.Id, BoardB, GroupRole.DungeonMaster);

        var playerHtml = await GetDetailsAsync(scenario.PlayerClient, scenario.QuestXId);
        playerHtml.Should().Contain("Bravo Linked Crypt Run");
        playerHtml.Should().NotContain("Bravo Secret Session Details");

        var dmHtml = await GetDetailsAsync(dmOnBClient, scenario.QuestXId);
        dmHtml.Should().Contain("Bravo Secret Session Details");
    }
}
