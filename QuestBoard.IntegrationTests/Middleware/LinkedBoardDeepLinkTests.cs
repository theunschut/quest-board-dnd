using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Middleware;

/// <summary>
/// A linked chip on the shared calendar is a plain link to the owning board's ordinary quest
/// page, and following it needs no code of its own: the existing deep-link middleware switches
/// a member to the owning board and shows the switch banner. These facts prove that in a harness
/// where the board switch is real, and prove the other half of the promise: a viewer who is not
/// a member is never given a link that would only end in a 404.
/// </summary>
public class LinkedBoardDeepLinkTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    // A fixed month far from the present, so no fact depends on what day it is when it runs.
    private const int Year = 2031;
    private const int Month = 3;
    private const string CalendarUrl = "/Calendar?year=2031&month=3";

    // Clean slate, board 1 linked with a new board B, and one open quest on B proposed for a day
    // in the fixed month. Returns B's quest id and the quest's title.
    private async Task<(int QuestId, string Title)> SeedLinkedBoardWithQuestAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"linkdm{suffix}", $"linkdm{suffix}@example.com", name: $"Linked DM {suffix}");

        var title = $"Linked Deep Link Quest {suffix}";
        await using var ctx = factory.Database.CreateContext();

        var set = new LinkedBoardSetEntity { Name = $"Deep Link Set {suffix}", BoardType = (int)BoardType.OneShot, CreatedAt = DateTime.UtcNow };
        ctx.LinkedBoardSets.Add(set);
        var boardB = new GroupEntity
        {
            Name = $"Deep Link Board B {suffix}",
            CreatedAt = DateTime.UtcNow,
            BoardType = (int)BoardType.OneShot
        };
        ctx.Groups.Add(boardB);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        boardB.LinkedBoardSetId = set.Id;
        var boardA = ctx.Groups.Single(g => g.Id == 1);
        boardA.LinkedBoardSetId = set.Id;
        boardA.BoardType = (int)BoardType.OneShot;
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var quest = new QuestEntity
        {
            Title = title,
            Description = "Lives on the linked board.",
            GroupId = boardB.Id,
            DungeonMasterId = dm.Id,
            ChallengeRating = 1,
            TotalPlayerCount = 4,
            CreatedAt = DateTime.UtcNow,
            ProposedDates = [new ProposedDateEntity { Date = new DateTime(Year, Month, 14, 19, 0, 0) }]
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (quest.Id, title);
    }

    private async Task AddMembershipOnBoardBAsync(int userId, string suffix)
    {
        await using var ctx = factory.Database.CreateContext();
        var boardBId = ctx.Groups.Single(g => g.Name == $"Deep Link Board B {suffix}").Id;
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SelectBoardOneAsync(HttpClient client)
    {
        var form = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage DesktopGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", DesktopUserAgent);
        return request;
    }

    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        return request;
    }

    private async Task<string> GetCalendarAsync(HttpClient client)
    {
        var response = await client.SendAsync(DesktopGet(CalendarUrl), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The linked entries markup of the calendar, from the first linked wrapper to the end.
    private static string LinkedEntries(string html)
    {
        var start = html.IndexOf("linked-day-entries", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        return html[start..];
    }

    [Fact]
    public async Task MemberOfBothBoards_FollowingALinkedChip_LandsOnTheOwningBoardWithTheSwitchBanner()
    {
        var (questId, title) = await SeedLinkedBoardWithQuestAsync("member");
        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "linkdeep_member", "linkdeep_member@example.com", roles: ["Player"]);
        await AddMembershipOnBoardBAsync(viewer.Id, "member");
        await SelectBoardOneAsync(client);

        var calendar = await GetCalendarAsync(client);

        var detailsUrl = $"/Quest/Details/{questId}";
        LinkedEntries(calendar).Should().Contain($"href=\"{detailsUrl}\"");

        var response = await client.SendAsync(NavigationGet(detailsUrl), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(title);
        body.Should().Contain("board-switch-toast");
    }

    [Fact]
    public async Task NonMemberOfTheLinkedBoard_SeesTheEntryWithNoLinkAndTheSameUrlAnswersNotFound()
    {
        var (questId, title) = await SeedLinkedBoardWithQuestAsync("nonmember");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "linkdeep_nonmember", "linkdeep_nonmember@example.com", roles: ["Player"]);
        await SelectBoardOneAsync(client);

        var calendar = await GetCalendarAsync(client);

        calendar.Should().Contain(title);
        calendar.Should().NotContain($"/Quest/Details/{questId}");

        var response = await client.SendAsync(NavigationGet($"/Quest/Details/{questId}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SuperAdminWithoutAMembershipOnTheLinkedBoard_IsGivenNoLinkToItsQuest()
    {
        var (questId, title) = await SeedLinkedBoardWithQuestAsync("superadmin");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "linkdeep_superadmin", "linkdeep_superadmin@example.com");
        await SelectBoardOneAsync(client);

        var calendar = await GetCalendarAsync(client);

        calendar.Should().Contain(title);
        calendar.Should().NotContain($"/Quest/Details/{questId}");
    }
}
