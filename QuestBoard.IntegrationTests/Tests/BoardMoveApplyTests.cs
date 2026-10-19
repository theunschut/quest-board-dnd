using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// A super admin's confirm POST moves an open quest to the other board of its linked set in
/// one save. Seeded: board 1 "Board Alpha" and board 2 "Board Bravo" linked, board 3 "Board
/// Charlie" outside the set, an open quest "Goblin Market" on board 1 run by a DM who is a
/// Dungeon Master on boards 1 and 2.
///
/// What these facts do NOT establish: how the write translates to SQL. The shared harness uses
/// the EF Core InMemory provider.
/// </summary>
public class BoardMoveApplyTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const int BoardCharlie = 3;
    private const string QuestTitle = "Goblin Market";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private record Scenario(int QuestId, HttpClient SuperAdminClient);

    private async Task<Scenario> SeedScenarioAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, BoardCharlie, "Board Charlie", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha, BoardBravo);

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_apply_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);

        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, QuestTitle, dm.Id,
            [new DateTime(2031, 5, 10, 19, 0, 0), new DateTime(2031, 5, 11, 19, 0, 0)]);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "move_apply_super", "move_apply_super@example.com");

        return new Scenario(questId, client);
    }

    private static FormUrlEncodedContent MoveForm(int questId, int targetBoardId, string stamp) =>
        new(new Dictionary<string, string>
        {
            ["id"] = questId.ToString(),
            ["targetBoardId"] = targetBoardId.ToString(),
            ["stamp"] = stamp
        });

    // The stamp the page would have carried: the service's own preview for the same target,
    // taken while the source board is the active one.
    private async Task<string> PreviewStampAsync(int questId, int targetBoardId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuestMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(questId, targetBoardId, actor.Id, TestContext.Current.CancellationToken);
        return preview.Stamp;
    }

    private async Task<HttpResponseMessage> PostMoveAsync(HttpClient client, int questId, int targetBoardId, string? stamp = null)
    {
        stamp ??= await PreviewStampAsync(questId, targetBoardId);
        return await client.PostAsync("/Move/Quest", MoveForm(questId, targetBoardId, stamp), TestContext.Current.CancellationToken);
    }

    private static string PathOf(HttpResponseMessage redirect)
    {
        var location = redirect.Headers.Location!.OriginalString;
        var query = location.IndexOf('?');
        return query < 0 ? location : location[..query];
    }

    private async Task<string> FollowAsync(HttpClient client, HttpResponseMessage redirect)
    {
        var location = redirect.Headers.Location!.ToString();
        var response = await client.GetAsync(location, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfirmPost_MovesTheQuestToTheLinkedBoardAndRaisesItsFeedRevisionByOne()
    {
        var scenario = await SeedScenarioAsync();
        var before = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        before.FeedRevision.Should().Be(1);

        var response = await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be("/quests");

        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("Moved \"Goblin Market\" to \"Board Bravo\".");

        var after = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        after.GroupId.Should().Be(BoardBravo);
        after.FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task AfterTheMove_TheQuestIsGoneFromTheSourceBoardAndShownOnTheTarget()
    {
        var scenario = await SeedScenarioAsync();
        var response = await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // The success toast names the quest, so it is shown and consumed before the board is checked.
        await FollowAsync(scenario.SuperAdminClient, response);

        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        var sourceDetails = await scenario.SuperAdminClient.GetAsync(
            $"/Quest/Details/{scenario.QuestId}", TestContext.Current.CancellationToken);
        sourceDetails.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var sourceBoard = await scenario.SuperAdminClient.GetAsync("/quests", TestContext.Current.CancellationToken);
        sourceBoard.StatusCode.Should().Be(HttpStatusCode.OK);
        (await sourceBoard.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().NotContain(QuestTitle);

        factory.TestGroupContext.ActiveGroupId = BoardBravo;
        var targetDetails = await scenario.SuperAdminClient.GetAsync(
            $"/Quest/Details/{scenario.QuestId}", TestContext.Current.CancellationToken);
        targetDetails.StatusCode.Should().Be(HttpStatusCode.OK);
        (await targetDetails.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Contain(QuestTitle);
    }

    [Theory]
    [InlineData(BoardCharlie)]
    [InlineData(BoardAlpha)]
    public async Task ConfirmPost_RefusesATargetOutsideTheSetOrTheSourceItself(int targetBoardId)
    {
        var scenario = await SeedScenarioAsync();

        var response = await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, targetBoardId);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be($"/Quest/Details/{scenario.QuestId}");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmPost_WithAStaleStamp_DoesNotMoveTheQuest()
    {
        var scenario = await SeedScenarioAsync();

        await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo, stamp: "stale");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmPost_ForAClosedQuest_IsRefusedAndTheQuestStaysOnItsBoard()
    {
        var scenario = await SeedScenarioAsync();
        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_closed_dm", "Closed DM");
        var closedId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Closed Tale", dm.Id, [new DateTime(2031, 5, 10, 19, 0, 0)], isClosed: true);

        var response = await PostMoveAsync(scenario.SuperAdminClient, closedId, BoardBravo, stamp: "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be($"/Quest/Details/{closedId}");
        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("This quest cannot be moved. It is closed in the Quest Log.");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, closedId);
        stored.GroupId.Should().Be(BoardAlpha);
    }

    [Fact]
    public async Task ConfirmPost_ForAPlayedQuest_IsRefusedAndTheQuestStaysOnItsBoard()
    {
        var scenario = await SeedScenarioAsync();
        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_played_dm", "Played DM");
        var playedId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Played Tale", dm.Id, [new DateTime(2020, 1, 10, 19, 0, 0)],
            finalizedDate: new DateTime(2020, 1, 10, 19, 0, 0));

        var response = await PostMoveAsync(scenario.SuperAdminClient, playedId, BoardBravo, stamp: "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be($"/Quest/Details/{playedId}");
        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("This quest cannot be moved. It has already been played.");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, playedId);
        stored.GroupId.Should().Be(BoardAlpha);
    }

    private const string GoneToast = "That quest is no longer on this board.";

    [Fact]
    public async Task ConfirmPost_RepeatedAfterASuccessfulMove_RedirectsToTheQuestBoardWithAnInfoToastAndMovesNothingAgain()
    {
        var scenario = await SeedScenarioAsync();
        var stamp = await PreviewStampAsync(scenario.QuestId, BoardBravo);
        var first = await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo, stamp);
        first.StatusCode.Should().Be(HttpStatusCode.Redirect);
        await FollowAsync(scenario.SuperAdminClient, first);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;

        var second = await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo, stamp);

        second.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(second).Should().Be("/quests");
        var html = await FollowAsync(scenario.SuperAdminClient, second);
        html.Should().Contain(GoneToast);

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardBravo);
        stored.FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task Get_AfterASuccessfulMove_RedirectsToTheQuestBoardWithAnInfoToastInsteadOfA404()
    {
        var scenario = await SeedScenarioAsync();
        var first = await PostMoveAsync(scenario.SuperAdminClient, scenario.QuestId, BoardBravo);
        await FollowAsync(scenario.SuperAdminClient, first);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;

        var back = await scenario.SuperAdminClient.GetAsync(
            $"/Move/Quest/{scenario.QuestId}", TestContext.Current.CancellationToken);

        back.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(back).Should().Be("/quests");
        (await FollowAsync(scenario.SuperAdminClient, back)).Should().Contain(GoneToast);
    }

    [Fact]
    public async Task GetAndPost_ForAQuestThatNeverExisted_AnswerLikeAQuestOnAnotherBoard()
    {
        var scenario = await SeedScenarioAsync();
        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_other_board_dm", "Other Board DM");
        var elsewhereId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardCharlie, "Hidden Tale", dm.Id, [new DateTime(2031, 5, 10, 19, 0, 0)]);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;

        foreach (var id in new[] { 99999, elsewhereId })
        {
            var get = await scenario.SuperAdminClient.GetAsync($"/Move/Quest/{id}", TestContext.Current.CancellationToken);
            get.StatusCode.Should().Be(HttpStatusCode.Redirect);
            PathOf(get).Should().Be("/quests");
            (await FollowAsync(scenario.SuperAdminClient, get)).Should().Contain(GoneToast);

            var post = await scenario.SuperAdminClient.PostAsync(
                "/Move/Quest", MoveForm(id, BoardBravo, "anything"), TestContext.Current.CancellationToken);
            post.StatusCode.Should().Be(HttpStatusCode.Redirect);
            PathOf(post).Should().Be("/quests");
            (await FollowAsync(scenario.SuperAdminClient, post)).Should().Contain(GoneToast);
        }

        (await BoardMoveSeed.ReadQuestAsync(factory, elsewhereId)).GroupId.Should().Be(BoardCharlie);
    }
}
