using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The target rules over HTTP. Seeded: board 1 "Board Alpha" (active), board 2 "Board Bravo"
/// linked to it, board 3 "Board Charlie" outside the set, and an open quest "Goblin Market" on
/// board 1 run by a DM who is a Dungeon Master on boards 1 and 2. A posted target outside the
/// set or the source itself moves nothing, a board in no set or a set of one never gets the
/// page, and a stale stamp re-shows the page instead of moving.
///
/// What these facts do NOT establish: how the reads translate to SQL. The shared harness uses
/// the EF Core InMemory provider.
/// </summary>
public class BoardMoveTargetTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const int BoardCharlie = 3;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private record Scenario(int QuestId, HttpClient SuperAdminClient, int SuperAdminId);

    // linkedBoards: the boards put into one set; none means no set at all.
    private async Task<Scenario> SeedScenarioAsync(params int[] linkedBoards)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, BoardCharlie, "Board Charlie", BoardType.OneShot);
        if (linkedBoards.Length > 0)
        {
            await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, linkedBoards);
        }

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_target_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);

        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", dm.Id,
            [new DateTime(2031, 5, 10, 19, 0, 0), new DateTime(2031, 5, 11, 19, 0, 0)]);

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "move_target_super", "move_target_super@example.com");

        return new Scenario(questId, client, user.Id);
    }

    private async Task<string> PreviewStampAsync(int questId, int targetBoardId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuestMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_target_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(questId, targetBoardId, actor.Id, TestContext.Current.CancellationToken);
        return preview.Stamp;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, int questId, int targetBoardId, string stamp) =>
        client.PostAsync("/Move/Quest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = questId.ToString(),
            ["targetBoardId"] = targetBoardId.ToString(),
            ["stamp"] = stamp
        }), TestContext.Current.CancellationToken);

    private static string PathOf(HttpResponseMessage redirect)
    {
        var location = redirect.Headers.Location!.OriginalString;
        var query = location.IndexOf('?');
        return query < 0 ? location : location[..query];
    }

    private static async Task<string> FollowAsync(HttpClient client, HttpResponseMessage redirect)
    {
        var response = await client.GetAsync(redirect.Headers.Location!.ToString(), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AStaleStamp_ReShowsThePageWithTheNoticeAndAFreshStampAndMovesNothing()
    {
        var scenario = await SeedScenarioAsync(BoardAlpha, BoardBravo);

        var response = await PostAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo, "stale");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.Should().Contain("Things changed while you were reviewing this.");
        html.Should().Contain("What will happen on Board Bravo");

        var freshStamp = await PreviewStampAsync(scenario.QuestId, BoardBravo);
        freshStamp.Should().NotBe("stale");
        html.Should().MatchRegex($"name=\"stamp\"[^>]*value=\"{Regex.Escape(freshStamp)}\"|value=\"{Regex.Escape(freshStamp)}\"[^>]*name=\"stamp\"");
        html.Should().NotContain("value=\"stale\"");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Theory]
    [InlineData(BoardCharlie)]
    [InlineData(BoardAlpha)]
    public async Task APostedTargetOutsideTheSetOrTheSourceItself_MovesNothingAndSaysSo(int targetBoardId)
    {
        var scenario = await SeedScenarioAsync(BoardAlpha, BoardBravo);

        var response = await PostAsync(scenario.SuperAdminClient, scenario.QuestId, targetBoardId, "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be($"/Quest/Details/{scenario.QuestId}");
        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("That board is not linked to this one, so nothing was moved.");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASourceBoardInNoSetOrASetOfOne_GetsNoPageAndMovesNothing(bool setOfOne)
    {
        var scenario = setOfOne
            ? await SeedScenarioAsync(BoardAlpha)
            : await SeedScenarioAsync();

        var get = await scenario.SuperAdminClient.GetAsync(
            $"/Move/Quest/{scenario.QuestId}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(get).Should().Be($"/Quest/Details/{scenario.QuestId}");
        var html = await FollowAsync(scenario.SuperAdminClient, get);
        html.Should().Contain("This board is not linked to any other board, so there is nowhere to move this.");

        var post = await PostAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo, "anything");
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(post).Should().Be($"/Quest/Details/{scenario.QuestId}");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Fact]
    public async Task AMoveByANonMemberOfTheTarget_ShowsTheSuccessToastOnTheQuestBoard()
    {
        var scenario = await SeedScenarioAsync(BoardAlpha, BoardBravo);
        var stamp = await PreviewStampAsync(scenario.QuestId, BoardBravo);

        var response = await PostAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo, stamp);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be("/quests");
        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("Moved \"Goblin Market\" to \"Board Bravo\".");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardBravo);
    }
}
