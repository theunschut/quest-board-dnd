using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Security;

/// <summary>
/// Proves the role check on a landed page is judged against the board the request was just
/// switched to, not the board that was active when the request began. The middleware runs before
/// authorization precisely so this is true; these facts are what make that pipeline position a
/// tested contract rather than a comment in Program.cs. Two directional facts pin both halves --
/// the role that must now pass, and the role that must still be refused -- and two further facts
/// prove the resolver itself never consults roles at all: an entity the landed page refuses to
/// serve still resolves and still switches the board, because the lookup only ever answers which
/// board owns an id, never whether the viewer should see what is there.
/// </summary>
public class CrossBoardAuthorizationBoundaryTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        return request;
    }

    // The viewer is a Player on Board A (group 1) and the quest's own Dungeon Master on Board B.
    // QuestController.Edit's ownership check (independent of the DungeonMasterOnly policy) also
    // requires the caller to be the quest's own DM or a board Admin -- making the viewer the
    // quest's DM is what isolates this fact to the policy question the plan asks, rather than
    // tangling it with the separate ownership question the action asks afterward.
    private async Task<(HttpClient client, int boardBId, int questId)> SeedViewerAsPlayerOnActiveBoardAndDmOwnerOnTargetBoardAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"boundarysucceed{suffix}", $"boundarysucceed{suffix}@example.com", roles: ["Player"]);

        int boardBId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"BoundarySucceedBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;
        }

        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, viewer.Id, title: $"BoundarySucceedQuest{suffix}", groupId: boardBId);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = boardBId, GroupRole = (int)GroupRole.DungeonMaster });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        return (client, boardBId, quest.Id);
    }

    // The viewer is the Dungeon Master on Board A (group 1) and only a Player on Board B --
    // the reverse of the fact above. The quest on Board B belongs to a different user entirely,
    // because the authorization policy fails before the action's own ownership check ever runs,
    // so who owns the quest cannot matter to this fact.
    private async Task<(HttpClient client, int boardBId, int questId)> SeedViewerAsDmOnActiveBoardAndPlayerOnTargetBoardAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var otherDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"boundaryrefusedm{suffix}", $"boundaryrefusedm{suffix}@example.com");

        int boardBId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"BoundaryRefuseBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;
        }

        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, otherDm.Id, title: $"BoundaryRefuseQuest{suffix}", groupId: boardBId);

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"boundaryrefuse{suffix}", $"boundaryrefuse{suffix}@example.com", roles: ["DungeonMaster"]);

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        return (client, boardBId, quest.Id);
    }

    [Fact]
    public async Task PlayerOnActiveBoard_DungeonMasterOnTargetBoard_ReachesTargetBoardsQuestEditPage()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, questId) = await SeedViewerAsPlayerOnActiveBoardAndDmOwnerOnTargetBoardAsync(suffix);

        var response = await client.SendAsync(NavigationGet($"/Quest/Edit/{questId}"), TestContext.Current.CancellationToken);

        // The policy was evaluated against Board B, not Board A -- the switch happened before
        // authorization ran. Moving the middleware's registration to after the authorization
        // call in Program.cs would make this fact fail, which is exactly the point: the pipeline
        // position is a tested contract here, not a comment.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"BoundarySucceedQuest{suffix}");

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain($"BoundarySucceedQuest{suffix}", because: "Board B must still be the active board on a following request");
    }

    [Fact]
    public async Task DungeonMasterOnActiveBoard_PlayerOnTargetBoard_IsRefusedTheQuestEditPage()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _, questId) = await SeedViewerAsDmOnActiveBoardAndPlayerOnTargetBoardAsync(suffix);

        var response = await client.SendAsync(NavigationGet($"/Quest/Edit/{questId}"), TestContext.Current.CancellationToken);

        // The lookup does not consult roles at all -- it answers only which board owns the id,
        // and the board switch happens regardless of the outcome the page then reaches. This is
        // precisely why the lookup can be audited in one file: it is never the thing deciding who
        // gets refused.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain($"BoundaryRefuseQuest{suffix}", because: "the board must have switched to Board B even though the page then refused the viewer");
    }

    [Fact]
    public async Task ResolvedCancelledEvent_SwitchesTheBoardAndLetsThePageDecideForItself()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"boundaryevent{suffix}", $"boundaryevent{suffix}@example.com", roles: ["Player"]);

        int boardBId, eventId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"BoundaryEventBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;

            var cancelledEvent = new EventEntity
            {
                Title = $"BoundaryCancelledEvent{suffix}",
                GroupId = boardBId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CreatedAt = DateTime.UtcNow,
                CancelledAt = DateTime.UtcNow.AddDays(-1)
            };
            ctx.Events.Add(cancelledEvent);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            eventId = cancelledEvent.Id;

            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        var response = await client.SendAsync(NavigationGet($"/Events/Details/{eventId}"), TestContext.Current.CancellationToken);

        // Resolution and the board switch are unconditional -- an event the calendar itself will
        // render as struck-through still resolves, still switches, and is left entirely to the
        // page's own rendering rules. Teaching the lookup this entity's visibility rule was
        // rejected: it would copy business logic into the tenancy bypass and require keeping it
        // in sync with every controller forever.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"BoundaryEventBoardB{suffix}", because: "the board-switch banner names the board the viewer landed on");
        body.Should().Contain("This session has been cancelled.");

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().NotContain("EuphoriaInn", because: "the board must not have half-switched back to Board A");
    }

    [Fact]
    public async Task ResolvedDraftShopItem_SwitchesTheBoardAndLetsThePageDecideForItself()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"boundaryshopdm{suffix}", $"boundaryshopdm{suffix}@example.com");
        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"boundaryshop{suffix}", $"boundaryshop{suffix}@example.com", roles: ["Player"]);

        int boardBId, shopItemId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var boardB = new GroupEntity { Name = $"BoundaryShopBoardB{suffix}", CreatedAt = DateTime.UtcNow };
            ctx.Groups.Add(boardB);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardBId = boardB.Id;

            // Draft status (0) -- an item the shop's own view refuses to offer for purchase,
            // unrelated to which board it lives on.
            var draftItem = new ShopItemEntity
            {
                Name = $"BoundaryDraftItem{suffix}",
                Description = "A test item seeded to prove resolution ignores the page's own visibility rules.",
                Type = 0,
                Rarity = 0,
                Price = 10m,
                Quantity = 1,
                Status = (int)ItemStatus.Draft,
                CreatedByDmId = dm.Id,
                GroupId = boardBId,
                CreatedAt = DateTime.UtcNow
            };
            ctx.ShopItems.Add(draftItem);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            shopItemId = draftItem.Id;

            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = boardBId, GroupRole = (int)GroupRole.Player });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var selectBoardAForm = new Dictionary<string, string> { ["groupId"] = "1" };
        await client.PostAsync("/GroupPicker/SelectGroup", new FormUrlEncodedContent(selectBoardAForm), TestContext.Current.CancellationToken);

        var response = await client.SendAsync(NavigationGet($"/Shop/Details/{shopItemId}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"BoundaryShopBoardB{suffix}", because: "the board-switch banner names the board the viewer landed on");
        body.Should().Contain("currently under review", because: "the shop's own Draft-status rule, not the resolver, is what withholds the purchase form");

        var followUp = await client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().NotContain("EuphoriaInn", because: "the board must not have half-switched back to Board A");
    }
}
