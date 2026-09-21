using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Security;

/// <summary>
/// Generalises the original tracer route's non-member/nonexistent-id parity fact across every
/// entity family the registry covers, plus a SuperAdmin, plus the two structural guarantees a
/// failed resolution must hold: it never leaves the board half-switched, and it never grows a
/// rendered error page a plain 404 does not already have.
///
/// What these facts do and do not establish: they prove the response to "exists on a board you
/// are not in" and the response to "does not exist" are indistinguishable to the requester in
/// status code, response body, and every header that is not itself a function of wall-clock time
/// or per-request tracing plumbing. They say nothing about timing -- a paired HTTP test cannot
/// see a timing side channel, and none of these facts claim to. The argument that covers timing
/// is structural: every family here reaches the same query shape through the same membership-
/// pinned repository method with no branch between "not a member" and "does not exist", so there
/// is nothing for a timing difference to hang off in the first place. That argument is made once,
/// in CrossBoardLinkRepository itself, not re-proven by measurement here.
/// </summary>
public class CrossBoardOracleParityTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    // Headers that legitimately vary between any two responses and therefore say nothing about
    // membership or existence: a wall-clock timestamp, and the two per-request correlation
    // headers ASP.NET Core's diagnostics pipeline can attach when distributed tracing is active.
    // Excluding them is what makes the remaining comparison mean something -- if every other
    // header still matched byte-for-byte, a real distinguishing signal would have nowhere to hide.
    private static readonly HashSet<string> VolatileHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date",
        "Request-Id",
        "traceparent"
    };

    // Board A (group 1, seeded by ClearDatabaseAsync) and Board B are both real memberships for
    // the viewer; Board C carries one row of every entity-backed family plus a member the viewer
    // does not share any board with, for the BoardMember/profile family. The viewer is Admin
    // everywhere they are a member, so no policy check in the routes under test can itself
    // distinguish the two requests -- the only thing that can differ is what the resolver does.
    private async Task<(HttpClient client, Dictionary<string, int> boardCIds)> SeedViewerOnBoardsAAndBWithEntitiesOnBoardCAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"paritydm{suffix}", $"paritydm{suffix}@example.com");
        var boardCOnlyUser = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"paritybconly{suffix}", $"paritybconly{suffix}@example.com", name: $"ParityBoardCOnlyUser{suffix}");

        int boardBId, boardCId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"ParityBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            var boardC = new GroupEntity { Name = $"ParityBoardC{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            ctx.Groups.Add(boardC);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;
            boardCId = boardC.Id;
        }

        // Finalized well in the past, matching CrossBoardRouteCoverageTests' seeding, so nothing
        // about the Quest Log's own "has this game night passed" gate can interfere here either.
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, title: $"ParityQuest{suffix}", groupId: boardCId,
            isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(-3));

        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, dm.Id, name: $"ParityCharacter{suffix}", groupId: boardCId);

        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dm.Id, name: $"ParityContact{suffix}", isRevealed: true, groupId: boardCId);

        var contactCategory = await TestDataHelper.CreateTestContactCategoryAsync(
            factory.Services, name: $"ParityCategory{suffix}", groupId: boardCId);

        int eventId, eventSeriesId, shopItemId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var newEvent = new EventEntity
            {
                Title = $"ParityEvent{suffix}",
                GroupId = boardCId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CreatedAt = DateTime.UtcNow
            };
            ctx.Events.Add(newEvent);

            var eventSeries = new EventSeriesEntity
            {
                Title = $"ParitySeries{suffix}",
                AnchorDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IntervalWeeks = 1,
                WeekDay = (int)DateTime.UtcNow.DayOfWeek,
                CycleMask = "1",
                GroupId = boardCId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.EventSeries.Add(eventSeries);

            var shopItem = new ShopItemEntity
            {
                Name = $"ParityShopItem{suffix}",
                Description = "A test item seeded for cross-board oracle parity.",
                Type = 0,
                Rarity = 0,
                Price = 10m,
                Quantity = 1,
                Status = 1,
                CreatedByDmId = dm.Id,
                GroupId = boardCId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.ShopItems.Add(shopItem);

            ctx.UserGroups.Add(new UserGroupEntity { UserId = boardCOnlyUser.Id, GroupId = boardCId, GroupRole = (int)GroupRole.DungeonMaster });

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            eventId = newEvent.Id;
            eventSeriesId = eventSeries.Id;
            shopItemId = shopItem.Id;
        }

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"parityviewer{suffix}", $"parityviewer{suffix}@example.com", roles: ["Admin"]);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Admin });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        var ids = new Dictionary<string, int>
        {
            ["Quest"] = quest.Id,
            ["Character"] = character.Id,
            ["Contact"] = contact.Id,
            ["ContactCategory"] = contactCategory.Id,
            ["Event"] = eventId,
            ["EventSeries"] = eventSeriesId,
            ["ShopItem"] = shopItemId,
            ["BoardMember"] = boardCOnlyUser.Id
        };

        return (client, ids);
    }

    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        return request;
    }

    private static Dictionary<string, string> CollectComparableHeaders(HttpResponseMessage response)
    {
        return response.Headers
            .Concat(response.Content.Headers)
            .Where(h => !VolatileHeaderNames.Contains(h.Key))
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
    }

    // One test, two requests, one assertion set -- two separate 404 tests could each describe a
    // different 404 and nothing would notice, whereas this fails the moment anyone introduces a
    // distinguishing branch between "exists but you're not a member" and "does not exist".
    private static async Task AssertIndistinguishableAsync(HttpResponseMessage nonMemberResponse, HttpResponseMessage nonexistentResponse)
    {
        nonMemberResponse.StatusCode.Should().Be(nonexistentResponse.StatusCode);

        var nonMemberBody = await nonMemberResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var nonexistentBody = await nonexistentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        nonMemberBody.Should().Be(nonexistentBody);

        CollectComparableHeaders(nonMemberResponse).Should().BeEquivalentTo(CollectComparableHeaders(nonexistentResponse));
    }

    // One route per family -- the registry's 18 routes reduce to 8 distinct lookup kinds, and a
    // pair per kind covers every code path a pair per route would, per this plan's own recorded
    // scoping decision. ContactCategory and BoardMember have no separate "Details" route, so their
    // one registered route (an edit page, a profile page) stands in for the family.
    public static IEnumerable<object[]> EntityFamilyRoutes()
    {
        yield return ["Quest", "/Quest/Details/{0}"];
        yield return ["Event", "/Events/Details/{0}"];
        yield return ["Character", "/Characters/Details/{0}"];
        yield return ["Contact", "/Contacts/Details/{0}"];
        yield return ["ShopItem", "/Shop/Details/{0}"];
        yield return ["EventSeries", "/Series/Details/{0}"];
        yield return ["ContactCategory", "/ContactCategoryManagement/Edit/{0}"];
        yield return ["BoardMember", "/DungeonMaster/Profile/{0}"];
    }

    [Theory]
    [MemberData(nameof(EntityFamilyRoutes))]
    public async Task CrossBoardOracleParity_EntityFamily_NonMemberAndNonexistentAreIndistinguishable(string family, string urlTemplate)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, ids) = await SeedViewerOnBoardsAAndBWithEntitiesOnBoardCAsync(suffix);
        var id = ids[family];

        var nonMemberResponse = await client.SendAsync(
            NavigationGet(string.Format(urlTemplate, id)), TestContext.Current.CancellationToken);
        var nonexistentResponse = await client.SendAsync(
            NavigationGet(string.Format(urlTemplate, int.MaxValue)), TestContext.Current.CancellationToken);

        await AssertIndistinguishableAsync(nonMemberResponse, nonexistentResponse);
    }

    [Fact]
    public async Task CrossBoardOracleParity_SuperAdmin_NonMemberAndNonexistentAreIndistinguishable()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, ids) = await SeedViewerOnBoardsAAndBWithEntitiesOnBoardCAsync(suffix);
        var questId = ids["Quest"];

        // The default seeding for a non-empty-roles caller (AuthenticationHelper) puts a
        // SuperAdmin on board one -- exactly the "active on Board A, not a member of Board C"
        // shape this fact needs, with no extra seeding required.
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, $"paritysuperadmin{suffix}", $"paritysuperadmin{suffix}@example.com");
        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await superAdminClient.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        var nonMemberResponse = await superAdminClient.SendAsync(
            NavigationGet($"/Quest/Details/{questId}"), TestContext.Current.CancellationToken);
        var nonexistentResponse = await superAdminClient.SendAsync(
            NavigationGet($"/Quest/Details/{int.MaxValue}"), TestContext.Current.CancellationToken);

        // Deliberate divergence from the rest of the application: the group picker and the
        // board gate both special-case SuperAdmin, but the resolver never does -- a cross-board
        // read that answered to a role rather than to a membership is exactly what this phase
        // exists to close off, so a SuperAdmin gets the same non-oracle pair as anyone else.
        await AssertIndistinguishableAsync(nonMemberResponse, nonexistentResponse);
    }

    [Fact]
    public async Task CrossBoardOracleParity_UnresolvableRequest_LeavesActiveBoardUnchanged()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, ids) = await SeedViewerOnBoardsAAndBWithEntitiesOnBoardCAsync(suffix);

        await client.SendAsync(NavigationGet($"/Quest/Details/{ids["Quest"]}"), TestContext.Current.CancellationToken);
        await client.SendAsync(NavigationGet($"/Quest/Details/{int.MaxValue}"), TestContext.Current.CancellationToken);

        // A failed resolution must never leave a half-applied switch behind -- proven the same
        // way the tracer and coverage suites prove it, with a following navigation still showing
        // Board A's own content.
        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain("EuphoriaInn", because: "Board A (group 1) must still be the active board after two unresolvable requests");
    }

    [Fact]
    public async Task CrossBoardOracleParity_UnresolvableEntity_Returns404WithEmptyBody()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await SeedViewerOnBoardsAAndBWithEntitiesOnBoardCAsync(suffix);

        var response = await client.SendAsync(NavigationGet($"/Quest/Details/{int.MaxValue}"), TestContext.Current.CancellationToken);

        // The application registers no status-code page middleware, so this is the bare
        // framework 404 -- an empty body, not a rendered error page. A rendered error page for
        // this case was considered and deliberately deferred: after this phase, a member
        // following a genuine cross-board link never reaches a 404 at all, and only a genuinely
        // nonexistent id still does, exactly as it does today.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();
    }
}
