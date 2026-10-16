using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The whole linked-boards story through the real paths: a super admin creates a set and adds two
/// boards with the Platform form posts, the viewer then sees the other board's session on every
/// calendar surface (desktop and phone calendar, desktop and phone Quest Details), a board outside
/// the set never shows, and removing the board through the Platform makes its entries vanish on
/// the viewer's very next requests. The other suites seed the set directly; this one does not.
///
/// What this fact does NOT establish: how the linked reads translate to SQL. The shared harness
/// uses the EF Core InMemory provider, so only the application's own scoping logic is exercised.
/// </summary>
public class LinkedBoardEndToEndTests(WebApplicationFactoryBase factory)
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
    private const string BravoSession = "BravoSession";
    private const string CharlieSession = "CharlieSession";

    // A fixed month far from the present, so the fact does not depend on the day it runs.
    private const int Year = 2031;
    private const int Month = 3;
    private const string CalendarUrl = "/Calendar?year=2031&month=3";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
            ctx.Groups.Add(new GroupEntity
            {
                Id = groupId,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                BoardType = (int)BoardType.OneShot
            });
            await ctx.SaveChangesAsync(Token);
        }
    }

    // One open quest on the named board proposing the 10th at 19:00, run by the given user.
    private async Task<int> SeedQuestAsync(int groupId, string title, int dmUserId)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "End to end linked boards quest",
            ChallengeRating = 5,
            DungeonMasterId = dmUserId,
            TotalPlayerCount = 4,
            GroupId = groupId,
            IsFinalized = false,
            CreatedAt = DateTime.UtcNow,
            ProposedDates = [new ProposedDateEntity { Date = new DateTime(Year, Month, 10, 19, 0, 0) }]
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(Token);
        return quest.Id;
    }

    private async Task SeedMembershipAsync(int userId, int groupId, GroupRole role)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role });
        await ctx.SaveChangesAsync(Token);
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.ToDictionary(f => f.Key, f => f.Value));

    private static async Task PostRedirectingAsync(HttpClient client, string url, params (string Key, string Value)[] fields)
    {
        var response = await client.PostAsync(url, Form(fields), Token);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    private static async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(Token);
    }

    private record Surfaces(string DesktopCalendar, string MobileCalendar, string DesktopDetails, string MobileDetails)
    {
        public IEnumerable<string> All => [DesktopCalendar, MobileCalendar, DesktopDetails, MobileDetails];
    }

    private static async Task<Surfaces> GetSurfacesAsync(HttpClient viewer, int questXId) => new(
        await GetAsync(viewer, CalendarUrl, DesktopUserAgent),
        await GetAsync(viewer, CalendarUrl, MobileUserAgent),
        await GetAsync(viewer, $"/Quest/Details/{questXId}", DesktopUserAgent),
        await GetAsync(viewer, $"/Quest/Details/{questXId}", MobileUserAgent));

    [Fact]
    public async Task LinkingThroughThePlatformShowsTheOtherBoardOnAllFourSurfacesAndUnlinkingRemovesItAtOnce()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardA;
        await SeedBoardAsync(BoardB, BoardBName);
        await SeedBoardAsync(BoardC, BoardCName);

        var (viewer, viewerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "e2e_viewer", "e2e_viewer@example.com", roles: ["Player"]);
        await SeedMembershipAsync(viewerUser.Id, BoardA, GroupRole.Player);
        await SeedMembershipAsync(viewerUser.Id, BoardB, GroupRole.Player);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "e2e_dm", "e2e_dm@example.com", name: "Dungeon Master Dana");
        var questXId = await SeedQuestAsync(BoardA, "Alpha Quest X", dm.Id);
        await SeedQuestAsync(BoardB, BravoSession, dm.Id);
        await SeedQuestAsync(BoardC, CharlieSession, dm.Id);

        var (superAdmin, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "e2e_superadmin", "e2e_superadmin@example.com");

        // Before any link the viewer sees only the own board.
        var before = await GetSurfacesAsync(viewer, questXId);
        foreach (var html in before.All)
        {
            html.Should().NotContain(BravoSession);
        }

        // Link: the set is created and filled through the Platform, not seeded.
        await PostRedirectingAsync(superAdmin, "/platform/LinkedBoards/Create",
            ("Name", "E2E Set"), ("BoardType", ((int)BoardType.OneShot).ToString()));

        int setId;
        await using (var ctx = factory.Database.CreateContext())
        {
            setId = (await ctx.LinkedBoardSets.SingleAsync(s => s.Name == "E2E Set", Token)).Id;
        }

        await PostRedirectingAsync(superAdmin, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", BoardA.ToString()));
        await PostRedirectingAsync(superAdmin, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", BoardB.ToString()));

        var linked = await GetSurfacesAsync(viewer, questXId);

        linked.DesktopCalendar.Should().Contain("linked-day-entries");
        linked.DesktopCalendar.Should().Contain(BravoSession);
        linked.MobileCalendar.Should().Contain("agenda-linked-entry");
        linked.MobileCalendar.Should().Contain(BravoSession);
        linked.DesktopDetails.Should().Contain("linked-day-entries");
        linked.DesktopDetails.Should().Contain(BravoSession);
        linked.DesktopDetails.Should().Contain($"Also showing: {BoardBName} (read-only)");
        linked.MobileDetails.Should().Contain("also-that-day");
        linked.MobileDetails.Should().Contain(BravoSession);
        foreach (var html in linked.All)
        {
            html.Should().NotContain(CharlieSession);
            html.Should().NotContain(BoardCName);
        }

        // Unlink: the viewer's very next requests show nothing of board B.
        await PostRedirectingAsync(superAdmin, $"/platform/LinkedBoards/RemoveBoard/{setId}", ("groupId", BoardB.ToString()));

        var unlinked = await GetSurfacesAsync(viewer, questXId);

        foreach (var html in unlinked.All)
        {
            html.Should().NotContain(BravoSession);
            html.Should().NotContain(CharlieSession);
            html.Should().NotContain("also-that-day");
            html.Should().NotContain("linked-day-entries");
            html.Should().NotContain("agenda-linked-entry");
            html.Should().NotContain("calendar-linked-note");
        }
    }
}
