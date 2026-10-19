using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The Move page a super admin opens before confirming. Seeded: board 1 "Board Alpha" (active),
/// board 2 "Board Bravo" linked to it, board 3 "Board Charlie" outside the set, and where a fact
/// needs several choices a board 4 "Board Delta" linked too. The quest's DM is a Dungeon Master
/// on every board of the set and every signup comes from a member of every board of the set.
///
/// What these facts do NOT establish: how the reads translate to SQL (the shared harness uses
/// the EF Core InMemory provider) or how the page looks in a browser.
/// </summary>
public class BoardMovePageTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const int BoardCharlie = 3;
    private const int BoardDelta = 4;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private record Scenario(int DmId, HttpClient SuperAdminClient);

    // Boards 1 and 2 are always linked; board 4 joins the set when asked for; board 3 never does.
    private async Task<Scenario> SeedScenarioAsync(bool linked = true, bool withDelta = false)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, BoardCharlie, "Board Charlie", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, BoardDelta, "Board Delta", BoardType.OneShot);

        var setBoards = withDelta ? new[] { BoardAlpha, BoardBravo, BoardDelta } : new[] { BoardAlpha, BoardBravo };
        if (linked)
        {
            await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, setBoards);
        }

        var dm = await BoardMoveSeed.CreateUserAsync(factory, "move_page_dm", "Dana the DM");
        foreach (var board in setBoards)
        {
            await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, board, GroupRole.DungeonMaster);
        }

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "move_page_super", "move_page_super@example.com");

        return new Scenario(dm.Id, client);
    }

    private static readonly DateTime FirstDate = new(2031, 5, 10, 19, 0, 0);
    private static readonly DateTime SecondDate = new(2031, 5, 11, 19, 0, 0);

    private async Task<(string Html, HttpResponseMessage Response)> GetPageAsync(
        HttpClient client, int questId, int? targetBoardId = null, string userAgent = DesktopUserAgent)
    {
        var url = targetBoardId == null ? $"/Move/Quest/{questId}" : $"/Move/Quest/{questId}?targetBoardId={targetBoardId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (html, response);
    }

    private async Task<string> FollowAsync(HttpClient client, HttpResponseMessage redirect)
    {
        var response = await client.GetAsync(redirect.Headers.Location!.ToString(), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string PathOf(HttpResponseMessage redirect)
    {
        var location = redirect.Headers.Location!.OriginalString;
        var query = location.IndexOf('?');
        return query < 0 ? location : location[..query];
    }

    private async Task<string> PreviewStampAsync(int questId, int targetBoardId)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IQuestMoveService>();
        var actor = await BoardMoveSeed.CreateUserAsync(factory, $"move_page_stamp_{Guid.NewGuid():N}", "Stamp Actor");
        var preview = await service.PreviewAsync(questId, targetBoardId, actor.Id, TestContext.Current.CancellationToken);
        return preview.Stamp;
    }

    [Fact]
    public async Task OneOtherBoard_ShowsTheMovingToLineThePreviewAndAConfirmFormCarryingTheServiceStamp()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate, SecondDate]);

        var (html, response) = await GetPageAsync(scenario.SuperAdminClient, questId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Move Quest to Linked Board");
        html.Should().Contain("Moving to Board Bravo");
        html.Should().NotContain("-- Select a board --");
        html.Should().Contain("What will happen on Board Bravo");
        html.Should().Contain("No signups to keep.");
        html.Should().Contain("The Dungeon Master and every player who stays are emailed that the quest has moved.");
        html.Should().Contain("Back to This Quest");
        html.Should().Contain("Move to Board Bravo");

        var stamp = await PreviewStampAsync(questId, BoardBravo);
        stamp.Should().NotBeNullOrEmpty();
        html.Should().MatchRegex($"name=\"stamp\"[^>]*value=\"{Regex.Escape(stamp)}\"|value=\"{Regex.Escape(stamp)}\"[^>]*name=\"stamp\"");
    }

    [Fact]
    public async Task TheKeptRow_CountsTheSignupsThatStay()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate]);
        var dateIds = await BoardMoveSeed.ProposedDateIdsAsync(factory, questId);
        foreach (var key in new[] { "move_page_p1", "move_page_p2" })
        {
            var player = await BoardMoveSeed.CreateUserAsync(factory, key, key);
            await BoardMoveSeed.SeedMembershipAsync(factory, player.Id, BoardAlpha, GroupRole.Player);
            await BoardMoveSeed.SeedMembershipAsync(factory, player.Id, BoardBravo, GroupRole.Player);
            await BoardMoveSeed.SeedSignupAsync(factory, questId, player.Id, SignupRole.Player, false, null,
                new Dictionary<int, VoteType> { [dateIds[0]] = VoteType.Yes }, DateTime.UtcNow);
        }

        var (html, _) = await GetPageAsync(scenario.SuperAdminClient, questId);

        html.Should().Contain("2 signups stay on the quest, with their role, selection and date votes.");
    }

    [Fact]
    public async Task SeveralOtherBoardsAndNoTarget_ShowsThePickerAndNoConfirmForm()
    {
        var scenario = await SeedScenarioAsync(withDelta: true);
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate]);

        var (html, response) = await GetPageAsync(scenario.SuperAdminClient, questId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("-- Select a board --");
        html.Should().Contain("Board Bravo");
        html.Should().Contain("Board Delta");
        html.Should().Contain("Show What Will Happen");
        html.Should().Contain("Choose a board to see what the move will do there.");
        html.Should().NotContain("What will happen on");
        html.Should().NotContain("action=\"/Move/Quest\"");
    }

    [Fact]
    public async Task SeveralOtherBoardsAndAChosenTarget_ShowsThePreviewForThatBoard()
    {
        var scenario = await SeedScenarioAsync(withDelta: true);
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate]);

        var (html, _) = await GetPageAsync(scenario.SuperAdminClient, questId, BoardDelta);

        html.Should().Contain("What will happen on Board Delta");
        html.Should().Contain("Move to Board Delta");
        html.Should().NotContain("Choose a board to see what the move will do there.");
    }

    [Fact]
    public async Task ATargetOutsideTheSet_ShowsTheNotInSetAlertAndThePickerWithNoConfirmForm()
    {
        var scenario = await SeedScenarioAsync(withDelta: true);
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate]);

        var (html, response) = await GetPageAsync(scenario.SuperAdminClient, questId, BoardCharlie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("That board is not part of this linked set. Choose one from the list.");
        html.Should().Contain("-- Select a board --");
        html.Should().NotContain("What will happen on");
        html.Should().NotContain("action=\"/Move/Quest\"");
    }

    [Fact]
    public async Task AFinalizedQuest_ShowsItsSessionAsAWallClockDate()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate], finalizedDate: FirstDate);

        var (html, _) = await GetPageAsync(scenario.SuperAdminClient, questId);

        html.Should().Contain("Session");
        html.Should().Contain("class=\"wall-clock\"");
        html.Should().Contain("datetime=\"2031-05-10T19:00\"");
        html.Should().Contain("Finalized");
    }

    [Theory]
    [InlineData(1, "Voting open, 1 proposed date")]
    [InlineData(2, "Voting open, 2 proposed dates")]
    public async Task AnOpenQuest_ShowsHowManyDatesAreProposed(int count, string expected)
    {
        var scenario = await SeedScenarioAsync();
        var dates = new[] { FirstDate, SecondDate }.Take(count).ToArray();
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, dates);

        var (html, _) = await GetPageAsync(scenario.SuperAdminClient, questId);

        html.Should().Contain(expected);
        html.Should().Contain("Open for signups");
    }

    [Fact]
    public async Task AClosedQuest_RedirectsToItsDetailsWithTheReason()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "Closed Tale", scenario.DmId, [FirstDate], isClosed: true);

        var (_, response) = await GetPageAsync(scenario.SuperAdminClient, questId);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be($"/Quest/Details/{questId}");
        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("This quest cannot be moved. It is closed in the Quest Log.");
    }

    [Fact]
    public async Task ABoardInNoSet_RedirectsToDetailsWithTheNotLinkedToast()
    {
        var scenario = await SeedScenarioAsync(linked: false);
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate]);

        var (_, response) = await GetPageAsync(scenario.SuperAdminClient, questId);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        PathOf(response).Should().Be($"/Quest/Details/{questId}");
        var html = await FollowAsync(scenario.SuperAdminClient, response);
        html.Should().Contain("This board is not linked to any other board, so there is nowhere to move this.");
    }

    [Fact]
    public async Task AQuestThatDoesNotExist_IsNotFound()
    {
        var scenario = await SeedScenarioAsync();

        var (_, response) = await GetPageAsync(scenario.SuperAdminClient, 99999);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AMobileUserAgent_GetsTheMobileCardsWithTheSameSentences()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(factory, BoardAlpha, "Goblin Market", scenario.DmId, [FirstDate, SecondDate]);

        var (html, response) = await GetPageAsync(scenario.SuperAdminClient, questId, userAgent: MobileUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("move-card-mobile");
        html.Should().Contain("Moving to Board Bravo");
        html.Should().Contain("What will happen on Board Bravo");
        html.Should().Contain("No signups to keep.");
        html.Should().Contain("Back to This Quest");
        html.Should().Contain("Move to Board Bravo");
        html.Should().Contain("d-flex gap-2");
    }

    [Fact]
    public async Task AQuestTitleWithMarkup_IsRenderedEncoded()
    {
        var scenario = await SeedScenarioAsync();
        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, "<b>Bold</b> Market", scenario.DmId, [FirstDate]);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Move/Quest/{questId}");
        var response = await scenario.SuperAdminClient.SendAsync(request, TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        raw.Should().Contain("&lt;b&gt;Bold&lt;/b&gt; Market");
        raw.Should().NotContain("<b>Bold</b>");
    }
}
