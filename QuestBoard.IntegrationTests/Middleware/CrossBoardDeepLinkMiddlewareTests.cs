using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Middleware;

/// <summary>
/// End-to-end proof, on one real route, that a member of two boards following a link to an
/// entity on the board they do not currently have active lands on the page with the board
/// switched and a one-shot banner, instead of the 404 the action returns today.
/// </summary>
public class CrossBoardDeepLinkMiddlewareTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    // Clean slate, two boards, one quest on each, and a viewer who belongs to both with Board A
    // selected as the active board -- the starting state every fact in this class needs.
    private async Task<(HttpClient client, int boardAQuestId, int boardBQuestId)> SeedAndSelectBoardAAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"tracerdm{suffix}", $"tracerdm{suffix}@example.com");

        int boardBId, boardAQuestId, boardBQuestId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"TracerBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;

            var boardAQuest = new QuestEntity
            {
                Title = $"TracerBoardAQuest{suffix}",
                Description = "Lives on Board A.",
                GroupId = 1,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            var boardBQuest = new QuestEntity
            {
                Title = $"TracerBoardBQuest{suffix}",
                Description = "Lives on Board B.",
                GroupId = boardBId,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Quests.Add(boardAQuest);
            ctx.Quests.Add(boardBQuest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardAQuestId = boardAQuest.Id;
            boardBQuestId = boardBQuest.Id;
        }

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"tracerviewer{suffix}", $"tracerviewer{suffix}@example.com", roles: ["Player"]);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        return (client, boardAQuestId, boardBQuestId);
    }

    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        return request;
    }

    [Fact]
    public async Task DeepLinkToOtherBoardsQuest_SwitchesBoardAndRendersOneShotBanner()
    {
        var (client, _, boardBQuestId) = await SeedAndSelectBoardAAsync("1");

        // Act — a real top-level navigation to a quest that lives on Board B while Board A is
        // active.
        var response = await client.SendAsync(NavigationGet($"/Quest/Details/{boardBQuestId}"), TestContext.Current.CancellationToken);

        // Assert — lands on the page (not the 404 this returns before the middleware exists),
        // carrying Board B's quest and the one-shot switch banner.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("TracerBoardBQuest1");
        body.Should().Contain("board-switch-toast");
        body.Should().Contain("TracerBoardB1");

        // Assert — the switch-back form carries exactly one antiforgery field and one hidden
        // groupId field, and nothing that could re-trigger the deep link that caused the switch.
        var toastStart = body.IndexOf("board-switch-toast", StringComparison.Ordinal);
        var formStart = body.IndexOf("<form", toastStart, StringComparison.Ordinal);
        var formEnd = body.IndexOf("</form>", formStart, StringComparison.Ordinal);
        var formMarkup = body[formStart..(formEnd + "</form>".Length)];

        formMarkup.Should().Contain("__RequestVerificationToken");
        formMarkup.Should().Contain("name=\"groupId\"");
        formMarkup.Should().NotContain("returnUrl");
        var inputCount = System.Text.RegularExpressions.Regex.Matches(formMarkup, "<input").Count;
        inputCount.Should().Be(2);

        // Act — a following GET of the same URL on the same client.
        var secondResponse = await client.SendAsync(NavigationGet($"/Quest/Details/{boardBQuestId}"), TestContext.Current.CancellationToken);
        var secondBody = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — the banner is one-shot: gone on the very next navigation.
        secondBody.Should().NotContain("board-switch-toast");
    }
}
