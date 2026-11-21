using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Security;

/// <summary>
/// A deep link into a record the viewer is not allowed to see must look exactly like a link to an
/// id that does not exist: the same status, the same headers and body, no move of the active board
/// and no "Switched board" notice on the next page. Otherwise a member could learn that a hidden
/// record exists, and on which of their boards. The resolver already withholds the switch where the
/// board does not offer the page or the viewer's role there could not open it; those facts run. The
/// facts that turn on the record itself (a draft shop item, an unrevealed contact, an unfinished
/// quest) stay skipped until the resolver applies each page's own visibility rule. The pins keep
/// what must keep working: a record the viewer may see switches the board and opens.
///
/// Every navigation carries the headers of a real top-level browser navigation, including
/// Accept: text/html, so the facts stay valid once error responses become rendered pages.
/// </summary>
public class CrossBoardHiddenRecordTests(CrossBoardWebApplicationFactory factory)
    : IClassFixture<CrossBoardWebApplicationFactory>
{
    private const string ActiveBoardName = "EuphoriaInn";

    private sealed record Scenario(HttpClient Client, string TargetBoardName, string Suffix, int OwnerId, int TargetBoardId);

    private static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Accept", "text/html");
        return request;
    }

    // The viewer is a member of the default board (active) and of a second board that holds the record. A second
    // client signs in as the same person and starts on the same board, so the nonexistent-id request is made from
    // the very state the hidden-record request starts from rather than from whatever the first request left behind.
    private async Task<Scenario> SeedAsync(string suffix, GroupRole roleOnActiveBoard, GroupRole roleOnTargetBoard, int disabledPagesOnTarget = 0)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"hiddenowner{suffix}", $"hiddenowner{suffix}@example.com");

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"hiddenviewer{suffix}", $"hiddenviewer{suffix}@example.com",
            roles: [roleOnActiveBoard == GroupRole.DungeonMaster ? "DungeonMaster" : "Player"]);

        var targetName = $"HiddenTargetBoard{suffix}";
        int targetBoardId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var target = new GroupEntity { Name = targetName, CreatedAt = DateTime.UtcNow, DisabledPages = disabledPagesOnTarget };
            ctx.Groups.Add(target);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetBoardId = target.Id;

            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = targetBoardId, GroupRole = (int)roleOnTargetBoard });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SelectActiveBoardAsync(client);
        return new Scenario(client, targetName, suffix, owner.Id, targetBoardId);
    }

    private static Task<HttpResponseMessage> SelectActiveBoardAsync(HttpClient client) =>
        client.PostAsync(
            "/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["groupId"] = "1" }),
            TestContext.Current.CancellationToken);

    private async Task<HttpClient> SignInAgainAsync(Scenario scenario)
    {
        var second = factory.CreateCookieClient();
        second.DefaultRequestHeaders.Authorization = scenario.Client.DefaultRequestHeaders.Authorization;
        await SelectActiveBoardAsync(second);
        return second;
    }

    private async Task<int> AddShopItemAsync(Scenario scenario, ItemStatus status, DateTime? availableFrom = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var item = new ShopItemEntity
        {
            Name = $"HiddenShopItem{scenario.Suffix}",
            Description = "A shop item seeded for the hidden-record facts.",
            Type = 0,
            Rarity = 0,
            Price = 10m,
            Quantity = 1,
            Status = (int)status,
            AvailableFrom = availableFrom,
            CreatedByDmId = scenario.OwnerId,
            GroupId = scenario.TargetBoardId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.ShopItems.Add(item);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item.Id;
    }

    // The profile rules look at who shares a board with the viewer, so the owner becomes a member of the target board
    // only (the default board is shared with nobody the facts name), which gives the profile link exactly one board
    // it can mean.
    private async Task AddOwnerToTargetBoardAsync(Scenario scenario)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = scenario.OwnerId, GroupId = scenario.TargetBoardId, GroupRole = (int)GroupRole.DungeonMaster });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AssertLooksLikeANonexistentIdAsync(Scenario scenario, string hiddenUrl, string nonexistentUrl)
    {
        var control = await SignInAgainAsync(scenario);
        await ErrorPageComparison.PrimeAsync(control, scenario.Client);

        var nonexistent = await control.SendAsync(NavigationGet(nonexistentUrl), TestContext.Current.CancellationToken);
        var hidden = await scenario.Client.SendAsync(NavigationGet(hiddenUrl), TestContext.Current.CancellationToken);

        await ErrorPageComparison.AssertIndistinguishableAsync(hidden, nonexistent);

        // The board the viewer was on is still the one they are on, and nothing on the next page announces a switch.
        var followUp = await scenario.Client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().Contain(ActiveBoardName, because: "the active board must be unchanged by a link to a record the viewer may not see");
        followUpBody.Should().NotContain(scenario.TargetBoardName);
        followUpBody.Should().NotContain("board-switch-toast", because: "no switch notice may follow a refused record");
    }

    // ---- Reproduction facts: the hidden record must look like a nonexistent id. Each one fails today. ----

    [Fact]
    public async Task HiddenRecord_DraftShopItem_ForAPlayerOfTheTargetBoard_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var itemId = await AddShopItemAsync(scenario, ItemStatus.Draft);

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/Shop/Details/{itemId}", $"/Shop/Details/{int.MaxValue}");
    }

    [Fact]
    public async Task HiddenRecord_DeniedShopItem_ForAPlayerOfTheTargetBoard_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var itemId = await AddShopItemAsync(scenario, ItemStatus.Denied);

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/Shop/Details/{itemId}", $"/Shop/Details/{int.MaxValue}");
    }

    [Fact]
    public async Task HiddenRecord_ScheduledShopItem_ForAPlayerOfTheTargetBoard_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var itemId = await AddShopItemAsync(scenario, ItemStatus.Published, availableFrom: DateTime.UtcNow.AddDays(30));

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/Shop/Details/{itemId}", $"/Shop/Details/{int.MaxValue}");
    }

    [Fact(Skip = "Fails until the cross-board resolver applies the target page's own visibility rule: a contact that is not revealed is shown only to its creator and to the board's Dungeon Masters and Admins.")]
    public async Task HiddenRecord_UnrevealedContactTheViewerDidNotCreate_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, scenario.OwnerId, name: $"HiddenContact{scenario.Suffix}", isRevealed: false, groupId: scenario.TargetBoardId);

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/Contacts/Details/{contact.Id}", $"/Contacts/Details/{int.MaxValue}");
    }

    [Fact(Skip = "Fails until the cross-board resolver applies the target page's own visibility rule: the quest log shows only quests that have been played or closed.")]
    public async Task HiddenRecord_OpenQuestOnTheQuestLog_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, scenario.OwnerId, title: $"HiddenOpenQuest{scenario.Suffix}", groupId: scenario.TargetBoardId);

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/QuestLog/Details/{quest.Id}", $"/QuestLog/Details/{int.MaxValue}");
    }

    [Fact]
    public async Task HiddenRecord_QuestEditOfAnotherDungeonMaster_ForAViewerWhoIsOnlyAPlayerOnTheTargetBoard_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.DungeonMaster, GroupRole.Player);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, scenario.OwnerId, title: $"HiddenEditQuest{scenario.Suffix}", groupId: scenario.TargetBoardId);

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/Quest/Edit/{quest.Id}", $"/Quest/Edit/{int.MaxValue}");
    }

    [Fact]
    public async Task HiddenRecord_ContactOnABoardThatDoesNotOfferTheContactsPage_LooksLikeANonexistentId()
    {
        var contactsOff = BoardPageSet.Empty.With(BoardPage.Contacts).Mask;
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player, disabledPagesOnTarget: contactsOff);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, scenario.OwnerId, name: $"HiddenPageContact{scenario.Suffix}", isRevealed: true, groupId: scenario.TargetBoardId);

        await AssertLooksLikeANonexistentIdAsync(scenario, $"/Contacts/Details/{contact.Id}", $"/Contacts/Details/{int.MaxValue}");
    }

    [Fact]
    public async Task HiddenRecord_ProfileEditOfAnotherMember_ForAViewerWhoIsOnlyAPlayerOnTheTargetBoard_LooksLikeANonexistentId()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.DungeonMaster, GroupRole.Player);
        await AddOwnerToTargetBoardAsync(scenario);

        await AssertLooksLikeANonexistentIdAsync(
            scenario, $"/DungeonMaster/EditProfile/{scenario.OwnerId}", $"/DungeonMaster/EditProfile/{int.MaxValue}");
    }

    [Fact]
    public async Task VisibleRecord_ProfileEditOfAnotherMember_ForAnAdminOfTheTargetBoard_SwitchesTheBoardAndOpens()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.DungeonMaster, GroupRole.Admin);
        await AddOwnerToTargetBoardAsync(scenario);

        var response = await scenario.Client.SendAsync(NavigationGet($"/DungeonMaster/EditProfile/{scenario.OwnerId}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var followUp = await scenario.Client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().NotContain(ActiveBoardName, because: "the board stays switched after the page opened");
    }

    // ---- Pins: a record the viewer may see keeps switching the board and opening. Green today, must stay green. ----

    [Fact]
    public async Task VisibleRecord_PublishedOpenedShopItem_ForAPlayerOfTheTargetBoard_SwitchesTheBoardAndOpens()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var itemId = await AddShopItemAsync(scenario, ItemStatus.Published);

        var response = await scenario.Client.SendAsync(NavigationGet($"/Shop/Details/{itemId}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"HiddenShopItem{scenario.Suffix}");
        body.Should().Contain(scenario.TargetBoardName, because: "the switch notice names the board the viewer landed on");

        var followUp = await scenario.Client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().NotContain(ActiveBoardName, because: "the board stays switched after the page opened");
    }

    [Fact]
    public async Task VisibleRecord_DraftShopItem_ForADungeonMasterOfTheTargetBoard_SwitchesTheBoardAndOpens()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.DungeonMaster);
        var itemId = await AddShopItemAsync(scenario, ItemStatus.Draft);

        var response = await scenario.Client.SendAsync(NavigationGet($"/Shop/Details/{itemId}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"HiddenShopItem{scenario.Suffix}");
        body.Should().Contain(scenario.TargetBoardName);

        var followUp = await scenario.Client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().NotContain(ActiveBoardName);
    }

    [Fact]
    public async Task VisibleRecord_RevealedContact_ForAMemberOfTheTargetBoard_SwitchesTheBoardAndOpens()
    {
        var scenario = await SeedAsync(Guid.NewGuid().ToString("N")[..8], GroupRole.Player, GroupRole.Player);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, scenario.OwnerId, name: $"VisibleContact{scenario.Suffix}", isRevealed: true, groupId: scenario.TargetBoardId);

        var response = await scenario.Client.SendAsync(NavigationGet($"/Contacts/Details/{contact.Id}"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain($"VisibleContact{scenario.Suffix}");
        body.Should().Contain(scenario.TargetBoardName);

        var followUp = await scenario.Client.SendAsync(NavigationGet("/quests"), TestContext.Current.CancellationToken);
        var followUpBody = await followUp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        followUpBody.Should().NotContain(ActiveBoardName);
    }
}
