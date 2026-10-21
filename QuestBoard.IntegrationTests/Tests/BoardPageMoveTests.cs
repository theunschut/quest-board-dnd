using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// A move onto a linked board that does not use that kind of entry is refused, on the page, on the
/// confirm and in the picker, and the move control is offered only when some linked board can take
/// the entry. Seeded: board 1 "Board Alpha" (active), board 2 "Board Bravo" linked to it, and where
/// a fact needs it board 3 "Board Charlie" in the same set; an open quest "Goblin Market" and an
/// upcoming event "Harvest Fair" on board 1.
///
/// What these facts do NOT establish: how the pages look, or how the reads translate to SQL. They
/// check markup and wording, and the shared harness uses the EF Core InMemory provider.
/// </summary>
public class BoardPageMoveTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const int BoardCharlie = 3;
    private const string ControlLabel = "Move to Linked Board";

    private static readonly DateTime[] UpcomingDates = [new(2031, 5, 10, 19, 0, 0), new(2031, 5, 11, 19, 0, 0)];
    private static readonly DateOnly UpcomingEventDate = new(2031, 5, 10);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardPageSeed.ResetAsync(factory, BoardAlpha);
        await BoardPageSeed.ResetAsync(factory, BoardBravo);
        await BoardPageSeed.ResetAsync(factory, BoardCharlie);
    }

    private record Scenario(HttpClient SuperAdminClient, int QuestId, int EventId);

    // linkedBoards: the boards put into one set, in addition to board 1.
    private async Task<Scenario> SeedScenarioAsync(params int[] otherLinkedBoards)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, BoardCharlie, "Board Charlie", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, [BoardAlpha, .. otherLinkedBoards]);

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "pagemove_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardCharlie, GroupRole.DungeonMaster);

        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", dm.Id, UpcomingDates);
        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, "Harvest Fair", UpcomingEventDate);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "pagemove_super", "pagemove_super@example.com");
        return new Scenario(client, questId, eventId);
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, WebUtility.HtmlDecode(html));
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string action, int id, int targetBoardId) =>
        client.PostAsync($"/Move/{action}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = id.ToString(),
            ["targetBoardId"] = targetBoardId.ToString(),
            ["stamp"] = "anything"
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
    public async Task EventPreview_TargetWithEventsOff_RedirectsToDetailsWithTheReason()
    {
        var scenario = await SeedScenarioAsync(BoardBravo);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);

        var get = await scenario.SuperAdminClient.GetAsync(
            $"/Move/Event/{scenario.EventId}?targetBoardId={BoardBravo}", TestContext.Current.CancellationToken);

        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(get).Should().Be($"/Events/Details/{scenario.EventId}");
        var html = await FollowAsync(scenario.SuperAdminClient, get);
        html.Should().Contain("Events aren't used on Board Bravo, so nothing can be moved there.");
    }

    [Fact]
    public async Task EventConfirm_TargetWithEventsOff_IsRefusedAndTheEventStaysPut()
    {
        var scenario = await SeedScenarioAsync(BoardBravo);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);

        var post = await PostAsync(scenario.SuperAdminClient, "Event", scenario.EventId, BoardBravo);

        post.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(post).Should().Be($"/Events/Details/{scenario.EventId}");
        var html = await FollowAsync(scenario.SuperAdminClient, post);
        html.Should().Contain("Events aren't used on Board Bravo, so nothing was moved.");

        var stored = await BoardMoveSeed.ReadEventAsync(factory, scenario.EventId);
        stored.GroupId.Should().Be(BoardAlpha);
    }

    [Fact]
    public async Task QuestPreview_TargetWithQuestsOff_RedirectsToDetailsWithTheReason()
    {
        var scenario = await SeedScenarioAsync(BoardBravo);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Calendar);

        var get = await scenario.SuperAdminClient.GetAsync(
            $"/Move/Quest/{scenario.QuestId}?targetBoardId={BoardBravo}", TestContext.Current.CancellationToken);

        get.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(get).Should().Be($"/Quest/Details/{scenario.QuestId}");
        var html = await FollowAsync(scenario.SuperAdminClient, get);
        html.Should().Contain("Quests aren't used on Board Bravo, so nothing can be moved there.");
    }

    [Fact]
    public async Task QuestConfirm_TargetWithQuestsOff_IsRefusedAndTheQuestStaysPut()
    {
        var scenario = await SeedScenarioAsync(BoardBravo);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Calendar);

        var post = await PostAsync(scenario.SuperAdminClient, "Quest", scenario.QuestId, BoardBravo);

        post.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(post).Should().Be($"/Quest/Details/{scenario.QuestId}");
        var html = await FollowAsync(scenario.SuperAdminClient, post);
        html.Should().Contain("Quests aren't used on Board Bravo, so nothing was moved.");

        var stored = await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId);
        stored.GroupId.Should().Be(BoardAlpha);
        stored.FeedRevision.Should().Be(1);
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task EventPicker_BoardWithEventsOff_IsADisabledOptionWithTheSuffix(string userAgent)
    {
        var scenario = await SeedScenarioAsync(BoardBravo, BoardCharlie);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);

        var (status, html) = await GetAsync(scenario.SuperAdminClient, $"/Move/Event/{scenario.EventId}", userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"<option value=\"{BoardBravo}\" disabled=\"disabled\">Board Bravo (events aren't used there)</option>");
        html.Should().Contain($"<option value=\"{BoardCharlie}\">Board Charlie</option>");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task QuestPicker_BoardWithQuestsOff_IsADisabledOptionWithTheSuffix(string userAgent)
    {
        var scenario = await SeedScenarioAsync(BoardBravo, BoardCharlie);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Calendar);

        var (status, html) = await GetAsync(scenario.SuperAdminClient, $"/Move/Quest/{scenario.QuestId}", userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"<option value=\"{BoardBravo}\" disabled=\"disabled\">Board Bravo (quests aren't used there)</option>");
        html.Should().Contain($"<option value=\"{BoardCharlie}\">Board Charlie</option>");
    }

    [Fact]
    public async Task EventDetails_OnlyLinkedBoardWithEventsOff_ShowsNoMoveControl()
    {
        var scenario = await SeedScenarioAsync(BoardBravo);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);

        var (status, html) = await GetAsync(scenario.SuperAdminClient, $"/Events/Details/{scenario.EventId}", DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain(ControlLabel);
    }

    [Fact]
    public async Task EventDetails_LinkedBoardWithEventsOn_ShowsTheMoveControl()
    {
        var scenario = await SeedScenarioAsync(BoardBravo);

        var (status, html) = await GetAsync(scenario.SuperAdminClient, $"/Events/Details/{scenario.EventId}", DesktopUserAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(ControlLabel);
        html.Should().Contain($"href=\"/Move/Event/{scenario.EventId}\"");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task QuestDetails_OnlyLinkedBoardWithQuestsOff_ShowsNoMoveControl(string userAgent)
    {
        var scenario = await SeedScenarioAsync(BoardBravo);
        await BoardPageSeed.SetAsync(factory, BoardBravo, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Calendar);

        var (status, html) = await GetAsync(scenario.SuperAdminClient, $"/Quest/Details/{scenario.QuestId}", userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain(ControlLabel);
    }
}
