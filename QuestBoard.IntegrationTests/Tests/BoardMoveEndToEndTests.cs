using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The whole move, walked the way a super admin lives it over HTTP: the Details control, the Move
/// page and its preview, the confirm with the page's own stamp, the toast with its link, the
/// server log line and what a calendar subscriber sees afterwards. Seeded: board 1 (the harness's own
/// "EuphoriaInn") and board 2 "Board Bravo", linked (one-shot), a super admin who is also a member of Bravo, and an
/// upcoming quest whose Dungeon Master runs both boards. Two members of both boards (one holding a
/// seat, one waitlisted) stay on the quest; a seated player who is only on the source board is removed.
///
/// What these facts do NOT establish: how the pages look, or how the writes translate to SQL. They
/// read markup and wording, and the shared harness uses the EF Core InMemory provider. The
/// deep-link switch onto the target board is proven elsewhere; here the toast link is read and the
/// target page is opened with the target board made active.
/// </summary>
public class BoardMoveEndToEndTests(MoveWebApplicationFactory factory)
    : IClassFixture<MoveWebApplicationFactory>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int BoardAlpha = 1;
    private const int BoardBravo = 2;
    private const string QuestTitle = "Goblin Market";
    private const string EventTitle = "Harvest Fair";

    // Inside the calendar feed's window, so the same quest can be read back from a feed.
    private static DateTime SessionDate => DateTime.Today.AddDays(5).AddHours(19);
    private static DateOnly EventDate => DateOnly.FromDateTime(DateTime.Today.AddDays(5));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private sealed record Scenario(
        int EntryId,
        UserEntity Dm,
        UserEntity K1,
        UserEntity K2,
        UserEntity D1,
        UserEntity Actor,
        HttpClient ActorClient);

    private async Task<(UserEntity Dm, UserEntity K1, UserEntity K2, UserEntity D1, UserEntity Actor, HttpClient Client)>
        SeedPeopleAsync(string key)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        factory.TestGroupContext.BoardType = BoardType.OneShot;

        await BoardMoveSeed.SeedBoardAsync(factory, BoardBravo, "Board Bravo", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(factory, BoardType.OneShot, BoardAlpha, BoardBravo);

        var dm = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_dm", "Dana the DM");
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardAlpha, GroupRole.DungeonMaster);
        await BoardMoveSeed.SeedMembershipAsync(factory, dm.Id, BoardBravo, GroupRole.DungeonMaster);

        var k1 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_k1", "Kept One");
        var k2 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_k2", "Kept Two");
        var d1 = await BoardMoveSeed.CreateUserAsync(factory, $"{key}_d1", "Dropped One");
        foreach (var kept in new[] { k1, k2 })
        {
            await BoardMoveSeed.SeedMembershipAsync(factory, kept.Id, BoardAlpha, GroupRole.Player);
            await BoardMoveSeed.SeedMembershipAsync(factory, kept.Id, BoardBravo, GroupRole.Player);
        }
        await BoardMoveSeed.SeedMembershipAsync(factory, d1.Id, BoardAlpha, GroupRole.Player);

        var (client, actor) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, $"{key}_actor", $"{key}_actor@example.com", name: "Move Actor");
        await BoardMoveSeed.SeedMembershipAsync(factory, actor.Id, BoardBravo, GroupRole.Admin);

        return (dm, k1, k2, d1, actor, client);
    }

    private async Task<Scenario> SeedQuestScenarioAsync(string key)
    {
        var (dm, k1, k2, d1, actor, client) = await SeedPeopleAsync(key);

        var questId = await BoardMoveSeed.SeedQuestAsync(
            factory, BoardAlpha, QuestTitle, dm.Id, [SessionDate], finalizedDate: SessionDate);
        var pdId = (await BoardMoveSeed.ProposedDateIdsAsync(factory, questId)).Single();
        var yes = new Dictionary<int, VoteType> { [pdId] = VoteType.Yes };
        var signedUpAt = DateTime.UtcNow.AddDays(-3);

        await BoardMoveSeed.SeedSignupAsync(factory, questId, k1.Id, SignupRole.Player, true, null, yes, signedUpAt);
        await BoardMoveSeed.SeedSignupAsync(factory, questId, k2.Id, SignupRole.Player, false, null, yes, signedUpAt);
        await BoardMoveSeed.SeedSignupAsync(factory, questId, d1.Id, SignupRole.Player, true, null, yes, signedUpAt);

        return new Scenario(questId, dm, k1, k2, d1, actor, client);
    }

    private async Task<Scenario> SeedEventScenarioAsync(string key)
    {
        var (dm, k1, k2, d1, actor, client) = await SeedPeopleAsync(key);

        var eventId = await BoardMoveSeed.SeedEventAsync(factory, BoardAlpha, EventTitle, EventDate);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, k1.Id, VoteType.Yes);
        await BoardMoveSeed.SeedEventSignupAsync(factory, eventId, d1.Id, VoteType.Maybe);

        return new Scenario(eventId, dm, k1, k2, d1, actor, client);
    }

    private static async Task<(HttpResponseMessage Response, string Html)> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    private static string FirstGroup(string html, string pattern, string what)
    {
        var match = Regex.Match(html, pattern);
        match.Success.Should().BeTrue(because: $"the page must carry {what}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed record WalkResult(string MovePageHtml, HttpResponseMessage ConfirmResponse, string ToastPageHtml);

    // The same steps a person takes: open Details, follow the control, read the preview, confirm
    // with the stamp and ids the page itself carries, then follow the redirect.
    private async Task<WalkResult> WalkAsync(
        Scenario scenario, string kind, string detailsPath, string movePath, string landingPath, string userAgent)
    {
        factory.TestGroupContext.ActiveGroupId = BoardAlpha;
        var client = scenario.ActorClient;
        var id = scenario.EntryId;

        var (detailsResponse, detailsHtml) = await GetAsync(client, $"{detailsPath}/{id}", userAgent);
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detailsHtml.Should().Contain("Move to Linked Board");
        var href = FirstGroup(detailsHtml, $"href=\"({Regex.Escape(movePath)}/\\d+)\"", "the Move to Linked Board link");
        href.Should().Be($"{movePath}/{id}");

        var (pageResponse, pageHtml) = await GetAsync(client, href, userAgent);
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        pageHtml.Should().Contain($"Move {kind} to Linked Board");
        WebUtility.HtmlDecode(pageHtml).Should().Contain("What will happen on Board Bravo");

        var formId = FirstGroup(pageHtml, "type=\"hidden\" name=\"id\" value=\"([^\"]*)\"", "the confirm form's id");
        var formTarget = FirstGroup(pageHtml, "type=\"hidden\" name=\"targetBoardId\" value=\"([^\"]*)\"", "the confirm form's target board");
        var formStamp = FirstGroup(pageHtml, "type=\"hidden\" name=\"stamp\" value=\"([^\"]*)\"", "the confirm form's stamp");
        formId.Should().Be(id.ToString());
        formTarget.Should().Be(BoardBravo.ToString());
        formStamp.Should().NotBeNullOrEmpty();

        var confirm = await client.PostAsync(movePath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = formId,
            ["targetBoardId"] = formTarget,
            ["stamp"] = formStamp
        }), TestContext.Current.CancellationToken);

        confirm.StatusCode.Should().Be(HttpStatusCode.Redirect);
        confirm.Headers.Location!.OriginalString.Should().BeEquivalentTo(landingPath);

        var (landingResponse, landingHtml) = await GetAsync(client, confirm.Headers.Location.OriginalString, userAgent);
        landingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return new WalkResult(pageHtml, confirm, landingHtml);
    }

    private Task<WalkResult> WalkQuestAsync(Scenario scenario, string userAgent = DesktopUserAgent) =>
        WalkAsync(scenario, "Quest", "/Quest/Details", "/Move/Quest", "/quests", userAgent);

    private Task<WalkResult> WalkEventAsync(Scenario scenario, string userAgent = DesktopUserAgent) =>
        WalkAsync(scenario, "Event", "/Events/Details", "/Move/Event", "/Events", userAgent);

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task SuperAdminMemberOfTheTarget_WalksTheWholeQuestMoveAndOpensTheQuestOnTheTargetBoard(string userAgent)
    {
        var scenario = await SeedQuestScenarioAsync($"e2e_quest_{(userAgent == MobileUserAgent ? "m" : "d")}");

        var walk = await WalkQuestAsync(scenario, userAgent);

        var page = WebUtility.HtmlDecode(walk.MovePageHtml);
        page.Should().Contain("Dropped One", because: "the preview names the player who will be removed");
        if (userAgent == MobileUserAgent)
        {
            walk.MovePageHtml.Should().Contain("move-card-mobile");
        }
        else
        {
            walk.MovePageHtml.Should().NotContain("move-card-mobile");
        }

        var toast = WebUtility.HtmlDecode(walk.ToastPageHtml);
        toast.Should().Contain("<strong class=\"me-auto\">Moved</strong>");
        toast.Should().Contain($"Moved \"{QuestTitle}\" to \"Board Bravo\".");
        toast.Should().Contain(" 1 signup was removed.");
        toast.Should().MatchRegex($"<a href=\"/Quest/Details/{scenario.EntryId}\" class=\"btn btn-sm btn-dark[^\"]*\">\\s*<i class=\"fas fa-arrow-right me-2\"[^>]*></i>Open it on Board Bravo");

        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.EntryId)).GroupId.Should().Be(BoardBravo);

        factory.TestGroupContext.ActiveGroupId = BoardBravo;
        var (target, targetHtml) = await GetAsync(scenario.ActorClient, $"/Quest/Details/{scenario.EntryId}", userAgent);
        target.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(targetHtml).Should().Contain(QuestTitle);
    }

    [Fact]
    public async Task SuperAdminMemberOfTheTarget_WalksTheWholeEventMoveAndOpensTheEventOnTheTargetBoard()
    {
        var scenario = await SeedEventScenarioAsync("e2e_event_walk");

        var walk = await WalkEventAsync(scenario);

        var toast = WebUtility.HtmlDecode(walk.ToastPageHtml);
        toast.Should().Contain("<strong class=\"me-auto\">Moved</strong>");
        toast.Should().Contain($"Moved \"{EventTitle}\" to \"Board Bravo\".");
        toast.Should().Contain(" 1 answer was removed.");
        toast.Should().MatchRegex($"<a href=\"/Events/Details/{scenario.EntryId}\" class=\"btn btn-sm btn-dark[^\"]*\">");
        toast.Should().Contain("Open it on Board Bravo");

        (await BoardMoveSeed.ReadEventAsync(factory, scenario.EntryId)).GroupId.Should().Be(BoardBravo);

        factory.TestGroupContext.ActiveGroupId = BoardBravo;
        var (target, targetHtml) = await GetAsync(scenario.ActorClient, $"/Events/Details/{scenario.EntryId}", DesktopUserAgent);
        target.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(targetHtml).Should().Contain(EventTitle);
    }

    private static readonly string[] MoveRecordMarkers = ["Moved quest", "Moved event", "move mail", "quest moved", "moved email"];

    private List<string> MoveRelatedRecords() =>
        factory.LogCapture.Records
            .Where(record => record.Contains("Move", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static void AssertNoPersonalData(IEnumerable<string> records, params UserEntity[] people)
    {
        foreach (var record in records)
        {
            foreach (var person in people)
            {
                record.Should().NotContain(person.Name, because: "the move's log records carry ids and counts, never a name");
                record.Should().NotContain(person.Email!, because: "the move's log records carry ids and counts, never an address");
            }
        }
    }

    [Fact]
    public async Task OneQuestMove_WritesExactlyOneAuditRecordWithTheTrueCountsAndNoPersonalData()
    {
        var scenario = await SeedQuestScenarioAsync("e2e_quest_audit");
        factory.LogCapture.Clear();

        await WalkQuestAsync(scenario);

        var expected = $"Moved quest {scenario.EntryId} from board {BoardAlpha} to board {BoardBravo} by user {scenario.Actor.Id}";
        var audit = factory.LogCapture.Records.Where(r => r.Contains(expected, StringComparison.Ordinal)).ToList();
        audit.Should().ContainSingle(because: "one move writes one structured record");
        audit.Single().Should().Contain("kept 2, dropped 1, promoted 1, links cut 0");

        var related = MoveRelatedRecords();
        related.Should().Contain(r => MoveRecordMarkers.Any(m => r.Contains(m, StringComparison.OrdinalIgnoreCase)));
        AssertNoPersonalData(related, scenario.Dm, scenario.K1, scenario.K2, scenario.D1);
    }

    [Fact]
    public async Task OneEventMove_WritesExactlyOneAuditRecordWithTheTrueCountsAndNoPersonalData()
    {
        var scenario = await SeedEventScenarioAsync("e2e_event_audit");
        factory.LogCapture.Clear();

        await WalkEventAsync(scenario);

        var expected = $"Moved event {scenario.EntryId} from board {BoardAlpha} to board {BoardBravo} by user {scenario.Actor.Id}";
        var audit = factory.LogCapture.Records.Where(r => r.Contains(expected, StringComparison.Ordinal)).ToList();
        audit.Should().ContainSingle(because: "one move writes one structured record");
        audit.Single().Should().Contain("kept 1, dropped 1, added 0");

        AssertNoPersonalData(MoveRelatedRecords(), scenario.Dm, scenario.K1, scenario.K2, scenario.D1);
    }

    private async Task<string> FetchFeedAsync(string token)
    {
        // A calendar client sends no cookie and has no active board.
        factory.TestGroupContext.ActiveGroupId = null;
        var response = await factory.CreateClient().GetAsync($"/feeds/calendar/{token}.ics", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string> MintTokenAsync(int userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        return (await service.MintForUserAsync(userId, TestContext.Current.CancellationToken)).Token;
    }

    [Fact]
    public async Task AfterAQuestMove_AKeptMembersFeedKeepsTheQuestUnderTheTargetBoardAndADroppedPlayersFeedLosesIt()
    {
        var scenario = await SeedQuestScenarioAsync("e2e_quest_feed");
        var k1Token = await MintTokenAsync(scenario.K1.Id);
        var d1Token = await MintTokenAsync(scenario.D1.Id);
        var uid = $"UID:questboard-quest-{scenario.EntryId}";

        var k1Before = await FetchFeedAsync(k1Token);
        var d1Before = await FetchFeedAsync(d1Token);
        k1Before.Should().Contain(uid);
        k1Before.Should().Contain("SEQUENCE:1\r\n");
        k1Before.Should().Contain($"SUMMARY:[EuphoriaInn] {QuestTitle}\r\n");
        d1Before.Should().Contain(uid);

        await WalkQuestAsync(scenario);

        var k1After = await FetchFeedAsync(k1Token);
        var d1After = await FetchFeedAsync(d1Token);
        k1After.Should().Contain(uid, because: "a kept member still holds a seat on the quest");
        k1After.Should().Contain("SEQUENCE:2\r\n", because: "the move is one revision of the same entry");
        k1After.Should().Contain($"SUMMARY:[Board Bravo] {QuestTitle}\r\n");
        k1After.Should().NotContain("[EuphoriaInn]");
        d1After.Should().NotContain(uid, because: "a removed player who is only on the source board can no longer see the quest");
    }
}
