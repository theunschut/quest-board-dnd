using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Middleware;

/// <summary>
/// End-to-end proof, over a real HTTP round trip, that every one of the 18 board-scoped routes
/// named by the phase's route list resolves across boards -- not merely that the registry
/// contains the right entries. Also proves the six image subresources and the shop item's modal
/// variant provably do not move the board, and that the two profile routes carry the ambiguity
/// rule rather than the plain membership-pinned lookup every other route uses.
/// </summary>
public class CrossBoardRouteCoverageTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    // Clean slate, two boards, and one row of every entity-backed lookup kind on Board B, plus a
    // user who is a member of Board B only (for the image-route facts, which need some id that
    // resolves the same way any other cross-board id would but through a route this feature
    // deliberately does not register). The viewer is a member of both boards with Admin on each,
    // so DungeonMasterOnly-gated edit/manage routes are reachable once the board has switched,
    // and Board A (group 1) is selected as the active board to start.
    private async Task<(HttpClient client, int boardBId, Dictionary<string, int> ids)> SeedTwoBoardsWithOneRowOfEachKindAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"coveragedm{suffix}", $"coveragedm{suffix}@example.com");
        var boardBOnlyUser = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"coveragebonly{suffix}", $"coveragebonly{suffix}@example.com", name: $"CoverageBoardBOnlyUser{suffix}");

        int boardBId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"CoverageBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;
        }

        // Finalized well in the past (not merely IsFinalized) so it is eligible for the Quest
        // Log's own "has this game night passed" gate, which QuestLog/Details and EditRecap both
        // require independently of the cross-board resolution being proven here.
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id,
            title: $"CoverageQuest{suffix}",
            isFinalized: true,
            finalizedDate: DateTime.UtcNow.AddDays(-3),
            groupId: boardBId);

        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, dm.Id, name: $"CoverageCharacter{suffix}", groupId: boardBId);

        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dm.Id, name: $"CoverageContact{suffix}", isRevealed: true, groupId: boardBId);

        var contactCategory = await TestDataHelper.CreateTestContactCategoryAsync(
            factory.Services, name: $"CoverageCategory{suffix}", groupId: boardBId);

        int eventId, eventSeriesId, shopItemId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var newEvent = new EventEntity
            {
                Title = $"CoverageEvent{suffix}",
                GroupId = boardBId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CreatedAt = DateTime.UtcNow
            };
            ctx.Events.Add(newEvent);

            var eventSeries = new EventSeriesEntity
            {
                Title = $"CoverageSeries{suffix}",
                AnchorDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IntervalWeeks = 1,
                WeekDay = (int)DateTime.UtcNow.DayOfWeek,
                CycleMask = "1",
                GroupId = boardBId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.EventSeries.Add(eventSeries);

            var shopItem = new ShopItemEntity
            {
                Name = $"CoverageShopItem{suffix}",
                Description = "A test item seeded for cross-board route coverage.",
                Type = 0,
                Rarity = 0,
                Price = 10m,
                Quantity = 1,
                Status = 1,
                CreatedByDmId = dm.Id,
                GroupId = boardBId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.ShopItems.Add(shopItem);

            ctx.UserGroups.Add(new UserGroupEntity { UserId = boardBOnlyUser.Id, GroupId = boardBId, GroupRole = (int)GroupRole.DungeonMaster });

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            eventId = newEvent.Id;
            eventSeriesId = eventSeries.Id;
            shopItemId = shopItem.Id;
        }

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"coverageviewer{suffix}", $"coverageviewer{suffix}@example.com", roles: ["Admin"]);

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
            ["BoardBOnlyUser"] = boardBOnlyUser.Id
        };

        return (client, boardBId, ids);
    }

    // Two boards and two target users: one who shares exactly Board B with the viewer (the
    // unambiguous case), and one who shares both Board A and Board B (the ambiguous case). The
    // viewer is a member of both boards, active on Board A.
    private async Task<(HttpClient client, int boardBId, int soleShareTargetUserId, int twoBoardTargetUserId)> SeedForProfileFactsAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        int boardBId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"ProfileBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;
        }

        var soleShareTarget = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"soletarget{suffix}", $"soletarget{suffix}@example.com", name: $"SoleShareTarget{suffix}");
        var twoBoardTarget = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"twotarget{suffix}", $"twotarget{suffix}@example.com", name: $"TwoBoardTarget{suffix}");

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = soleShareTarget.Id, GroupId = boardBId, GroupRole = (int)GroupRole.DungeonMaster });
            ctx.UserGroups.Add(new UserGroupEntity { UserId = twoBoardTarget.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
            ctx.UserGroups.Add(new UserGroupEntity { UserId = twoBoardTarget.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"profileviewer{suffix}", $"profileviewer{suffix}@example.com", roles: ["Admin"]);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Admin });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        return (client, boardBId, soleShareTarget.Id, twoBoardTarget.Id);
    }

    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
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

    // The 16 entity-backed routes -- the 18 minus the two profile routes, which carry a
    // different rule and are covered separately below. "kind" selects which seeded id this row
    // exercises; expectedTextTemplate is a {0}-formatted fragment the read routes must render,
    // and is null for the edit/manage routes, whose only obligation here is "not 404".
    public static IEnumerable<object?[]> EntityBackedRoutes()
    {
        yield return ["Quest", "Details", "Quest", true, "CoverageQuest{0}"];
        yield return ["Quest", "Edit", "Quest", false, null];
        yield return ["Quest", "Manage", "Quest", false, null];
        yield return ["Quest", "CreateFollowUp", "Quest", false, null];
        yield return ["QuestLog", "Details", "Quest", true, "CoverageQuest{0}"];
        yield return ["QuestLog", "EditRecap", "Quest", false, null];
        yield return ["Events", "Details", "Event", true, "CoverageEvent{0}"];
        yield return ["Events", "Edit", "Event", false, null];
        yield return ["Characters", "Details", "Character", true, "CoverageCharacter{0}"];
        yield return ["Characters", "Edit", "Character", false, null];
        yield return ["Contacts", "Details", "Contact", true, "CoverageContact{0}"];
        yield return ["Contacts", "Edit", "Contact", false, null];
        yield return ["Shop", "Details", "ShopItem", true, "CoverageShopItem{0}"];
        yield return ["ShopManagement", "Edit", "ShopItem", false, null];
        yield return ["Series", "Details", "EventSeries", true, "CoverageSeries{0}"];
        yield return ["ContactCategoryManagement", "Edit", "ContactCategory", false, null];
    }

    [Theory]
    [MemberData(nameof(EntityBackedRoutes))]
    public async Task CrossBoardRouteCoverage_EntityBackedRoute_ResolvesAcrossBoards(
        string controller, string action, string kind, bool isRead, string? expectedTextTemplate)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, ids) = await SeedTwoBoardsWithOneRowOfEachKindAsync(suffix);
        var id = ids[kind];

        var response = await client.SendAsync(NavigationGet($"/{controller}/{action}/{id}"), TestContext.Current.CancellationToken);

        // Whether the page then renders or refuses is the page's own business -- the resolver's
        // only job is to have switched the board so the action's query filter can see the row at
        // all, instead of the 404 it would return today.
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            because: $"{controller}/{action} should have resolved across boards instead of 404ing");

        if (isRead)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.Should().Contain(string.Format(expectedTextTemplate!, suffix));
        }
    }

    // The six image subresources arrive with a non-document fetch destination and fail the
    // navigation gate on their own -- proven here by asserting the board is unchanged, not
    // merely that the response looks ordinary, since an unchanged board is what a 404 for a
    // Board B id under an active Board A session actually requires.
    public static IEnumerable<object[]> ImageRoutes()
    {
        yield return ["Characters", "GetProfilePicture", "Character"];
        yield return ["Characters", "GetCroppedPicture", "Character"];
        yield return ["Contacts", "GetContactImage", "Contact"];
        yield return ["Contacts", "GetCroppedContactImage", "Contact"];
        yield return ["DungeonMaster", "GetDMProfilePicture", "BoardBOnlyUser"];
        yield return ["DungeonMaster", "GetOriginalDMProfilePicture", "BoardBOnlyUser"];
    }

    [Theory]
    [MemberData(nameof(ImageRoutes))]
    public async Task CrossBoardRouteCoverage_ImageRoute_LeavesActiveBoardUnchanged(string controller, string action, string kind)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, ids) = await SeedTwoBoardsWithOneRowOfEachKindAsync(suffix);
        var id = ids[kind];

        var imageHeaders = new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "image", ["Sec-Fetch-Mode"] = "no-cors" };
        var response = await client.SendAsync(RequestWithHeaders($"/{controller}/{action}/{id}", imageHeaders), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain("EuphoriaInn", because: "Board A (group 1) must still be the active board");
    }

    // Shop/Details's isModal AJAX variant arrives as Sec-Fetch-Dest: empty and must not move the
    // board, while the full-page variant of the exact same URL does.
    [Fact]
    public async Task CrossBoardRouteCoverage_ShopItemModalVariant_LeavesActiveBoardUnchanged_WhileFullPageVariantSwitches()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, ids) = await SeedTwoBoardsWithOneRowOfEachKindAsync(suffix);
        var shopItemId = ids["ShopItem"];

        var modalHeaders = new Dictionary<string, string> { ["Sec-Fetch-Dest"] = "empty", ["Sec-Fetch-Mode"] = "cors" };
        var modalResponse = await client.SendAsync(RequestWithHeaders($"/Shop/Details/{shopItemId}?isModal=true", modalHeaders), TestContext.Current.CancellationToken);
        modalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain("EuphoriaInn", because: "the modal fetch must not have moved the board");

        var fullPageResponse = await client.SendAsync(NavigationGet($"/Shop/Details/{shopItemId}"), TestContext.Current.CancellationToken);
        fullPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fullPageBody = await fullPageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        fullPageBody.Should().Contain($"CoverageShopItem{suffix}");
    }

    [Fact]
    public async Task CrossBoardRouteCoverage_ProfileLink_UnambiguousSharedBoard_SwitchesAndRenders()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, soleShareTargetUserId, _) = await SeedForProfileFactsAsync(suffix);

        var response = await client.SendAsync(NavigationGet($"/DungeonMaster/Profile/{soleShareTargetUserId}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("board-switch-toast");
        body.Should().Contain($"ProfileBoardB{suffix}");
    }

    [Fact]
    public async Task CrossBoardRouteCoverage_ProfileLink_TwoSharedBoards_LeavesActiveBoardUnchanged()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, _, twoBoardTargetUserId) = await SeedForProfileFactsAsync(suffix);

        var response = await client.SendAsync(NavigationGet($"/DungeonMaster/Profile/{twoBoardTargetUserId}"), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("board-switch-toast");

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain("EuphoriaInn", because: "sharing two boards has no unambiguous answer, so today's behaviour must stand");
        followUpBody.Should().NotContain($"ProfileBoardB{suffix}");
    }

    [Fact]
    public async Task CrossBoardRouteCoverage_ProfileEditWithNoId_LeavesActiveBoardUnchanged()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, _, _) = await SeedForProfileFactsAsync(suffix);

        var response = await client.SendAsync(NavigationGet("/DungeonMaster/EditProfile"), TestContext.Current.CancellationToken);
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain("EuphoriaInn", because: "a route with no id to resolve is a self-edit and never reaches the resolver");
        followUpBody.Should().NotContain($"ProfileBoardB{suffix}");
    }
}
