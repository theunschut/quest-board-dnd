using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Middleware;

/// <summary>
/// Pins the harness itself, not any feature built on top of it, so these assertions must stay
/// true no matter what the rest of this phase adds. Deliberately uses a route with no id and
/// therefore no place in any cross-board registry -- the quest list at /quests -- so nothing
/// later in this phase can make these facts stale.
/// </summary>
public class CrossBoardTestHarnessTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    // Clean slate, two boards, one quest on each with a distinctive title, and one user who is a
    // member of both. Returns the authenticated client (reused for every hop in a test) and
    // Board B's generated id (Board A reuses the default seeded group, id 1).
    private async Task<(HttpClient client, int boardBId)> SeedTwoBoardsWithAMemberOfBothAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"harnessdm{suffix}", $"harnessdm{suffix}@example.com");

        int boardBId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"HarnessBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;

            ctx.Quests.Add(new QuestEntity
            {
                Title = $"HarnessBoardAQuest{suffix}",
                Description = "Lives on Board A.",
                GroupId = 1,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Quests.Add(new QuestEntity
            {
                Title = $"HarnessBoardBQuest{suffix}",
                Description = "Lives on Board B.",
                GroupId = boardBId,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // CreateAuthenticatedClientWithUserAsync seeds membership on Board A (group 1) since
        // roles is left at its default (null). Add membership on Board B too.
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"harnessviewer{suffix}", $"harnessviewer{suffix}@example.com", roles: ["Player"]);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return (client, boardBId);
    }

    [Fact]
    public async Task NoBoardSelectedYet_RedirectsToGroupPicker()
    {
        var (client, _) = await SeedTwoBoardsWithAMemberOfBothAsync("1");

        // With no board selected yet, the session-backed context is really null, not defaulting
        // to board 1: the request is redirected to the group picker.
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("/groups/pick");
        location.Should().Contain("returnUrl=");
    }

    [Fact]
    public async Task SelectingBoardA_ShowsOnlyBoardAsQuest()
    {
        var (client, _) = await SeedTwoBoardsWithAMemberOfBothAsync("2");

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        var selectResponse = await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);
        selectResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("HarnessBoardAQuest2");
        body.Should().NotContain("HarnessBoardBQuest2");
    }

    [Fact]
    public async Task SwitchingToBoardBOnTheSameClient_ShowsOnlyBoardBsQuest()
    {
        var (client, boardBId) = await SeedTwoBoardsWithAMemberOfBothAsync("3");

        // Select Board A first, exactly like a real session would after login.
        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        // This is the fact that fails under the default harness and passes under this one:
        // selecting Board B on the same client and requesting /quests again must now show
        // Board B's quest instead of Board A's. Under WebApplicationFactoryBase's singleton
        // MutableGroupContext, this session write is invisible to the query filters and the
        // response would still show Board A's quest.
        var selectBoardBForm = new Dictionary<string, string> { ["groupId"] = boardBId.ToString() };
        var selectBoardBResponse = await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(selectBoardBForm), TestContext.Current.CancellationToken);
        selectBoardBResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("HarnessBoardBQuest3");
        body.Should().NotContain("HarnessBoardAQuest3");
    }
}
