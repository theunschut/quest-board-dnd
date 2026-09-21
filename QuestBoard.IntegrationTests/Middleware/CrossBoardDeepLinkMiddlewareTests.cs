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

    // Clean slate, two boards, one quest on each, and a viewer who belongs to Board A only, with
    // Board A active -- the starting state the oracle-parity fact needs, where the viewer must
    // genuinely not be a member of Board B.
    private async Task<(HttpClient client, int boardBQuestId)> SeedWithViewerOnBoardAOnlyAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"paritydm{suffix}", $"paritydm{suffix}@example.com");

        int boardBQuestId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"ParityBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

            var boardBQuest = new QuestEntity
            {
                Title = $"ParityBoardBQuest{suffix}",
                Description = "Lives on Board B.",
                GroupId = boardB.Id,
                DungeonMasterId = dm.Id,
                ChallengeRating = 1,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Quests.Add(boardBQuest);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBQuestId = boardBQuest.Id;
        }

        // roles: ["Player"] seeds Board A (group 1) membership only -- deliberately no Board B row.
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"parityviewer{suffix}", $"parityviewer{suffix}@example.com", roles: ["Player"]);

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        return (client, boardBQuestId);
    }

    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        return request;
    }

    private static HttpRequestMessage NavigationPost(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Content = new FormUrlEncodedContent([]);
        return request;
    }

    private static HttpRequestMessage RequestWithHeaders(string url, IReadOnlyDictionary<string, string> headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }
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

    // What this gate does and does not do: it keeps a viewer's own browser background requests
    // (an image load, a prefetch, a prerender, a link unfurl) from moving their board. It is not
    // a defence against a hostile HTTP client, which can set any header value it likes -- the
    // tenancy guarantee comes from the membership-pinned lookup, proven separately by the oracle
    // parity fact below, not from these headers.
    public static IEnumerable<object[]> NonNavigationHeaderCombinations()
    {
        yield return [new Dictionary<string, string>()];
        yield return [new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "image", ["Sec-Fetch-Mode"] = "no-cors" }];
        yield return [new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "empty", ["Sec-Fetch-Mode"] = "cors" }];
        yield return [new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "document", ["Sec-Fetch-Mode"] = "navigate", ["Sec-Purpose"] = "prefetch" }];
        yield return [new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "document", ["Sec-Fetch-Mode"] = "navigate", ["Purpose"] = "prefetch" }];
        yield return [new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "document", ["Sec-Fetch-Mode"] = "navigate", ["X-Moz"] = "prefetch" }];
    }

    [Theory]
    [MemberData(nameof(NonNavigationHeaderCombinations))]
    public async Task NonNavigationRequest_LeavesActiveBoardUnchanged(Dictionary<string, string> headers)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, boardBQuestId) = await SeedAndSelectBoardAAsync(suffix);

        var response = await client.SendAsync(RequestWithHeaders($"/Quest/Details/{boardBQuestId}", headers), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The active board must be exactly what it was before this request -- proven by a real
        // navigation to the quest board still showing Board A's quest and not Board B's.
        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain($"TracerBoardAQuest{suffix}");
        followUpBody.Should().NotContain($"TracerBoardBQuest{suffix}");
    }

    [Fact]
    public async Task PostToOtherBoardsQuest_LeavesActiveBoardUnchanged()
    {
        var (client, boardAQuestId, boardBQuestId) = await SeedAndSelectBoardAAsync("verb");

        // Control: an identically-shaped POST against a Board A URL, to capture what "today's
        // response" actually is without hard-coding an assumption about the status code.
        var controlResponse = await client.SendAsync(NavigationPost($"/Quest/Details/{boardAQuestId}"), TestContext.Current.CancellationToken);

        var probeResponse = await client.SendAsync(NavigationPost($"/Quest/Details/{boardBQuestId}"), TestContext.Current.CancellationToken);

        probeResponse.StatusCode.Should().Be(controlResponse.StatusCode);
        ((int)probeResponse.StatusCode / 100).Should().NotBe(2, because: "a POST must never be treated as the trigger for a board switch");

        // The active board must not have moved -- proven the same way, with a following GET
        // showing Board A still active.
        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain("TracerBoardAQuestverb");
        followUpBody.Should().NotContain("TracerBoardBQuestverb");
    }

    // One test, two requests, one assertion set: two separate 404 tests can each describe a
    // different 404 and nothing would notice, whereas this fails the moment anyone introduces a
    // distinguishing branch between "exists but you're not a member" and "does not exist".
    [Fact]
    public async Task NonMemberRequest_AndNonexistentId_ProduceIndistinguishableResponses()
    {
        var (client, boardBQuestId) = await SeedWithViewerOnBoardAOnlyAsync("oracle");

        var nonMemberResponse = await client.SendAsync(NavigationGet($"/Quest/Details/{boardBQuestId}"), TestContext.Current.CancellationToken);
        var nonexistentResponse = await client.SendAsync(NavigationGet($"/Quest/Details/{int.MaxValue}"), TestContext.Current.CancellationToken);

        nonMemberResponse.StatusCode.Should().Be(nonexistentResponse.StatusCode);
        nonMemberResponse.Content.Headers.ContentType.Should().BeEquivalentTo(nonexistentResponse.Content.Headers.ContentType);

        var nonMemberBody = await nonMemberResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var nonexistentBody = await nonexistentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        nonMemberBody.Should().Be(nonexistentBody);
    }

    // The structural guarantee bought by dropping the return path from the switch-back form:
    // clicking it can never land back on the deep link that triggered the original switch.
    [Fact]
    public async Task SwitchBack_NeverReturnsToTheDeepLinkThatTriggeredTheSwitch()
    {
        var (client, _, boardBQuestId) = await SeedAndSelectBoardAAsync("pingpong");

        // Trigger the auto-switch to Board B.
        await client.SendAsync(NavigationGet($"/Quest/Details/{boardBQuestId}"), TestContext.Current.CancellationToken);

        // Board A is group 1 in every fact in this class -- the switch-back form's hidden
        // groupId field carries exactly this value.
        var switchBackForm = new Dictionary<string, string> { ["groupId"] = "1" };
        var switchBackResponse = await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(switchBackForm), TestContext.Current.CancellationToken);

        switchBackResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var redirectLocation = switchBackResponse.Headers.Location?.ToString() ?? string.Empty;
        redirectLocation.Should().NotBe($"/Quest/Details/{boardBQuestId}");

        var landedResponse = await client.SendAsync(NavigationGet(redirectLocation), TestContext.Current.CancellationToken);
        var landedBody = await landedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        landedBody.Should().Contain("TracerBoardAQuestpingpong");
    }
}
