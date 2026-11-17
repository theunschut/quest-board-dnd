using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Controllers;

// A shop item belongs to a board: ShopItems.GroupId is a required foreign key to Groups and the
// board query filter hides every item whose GroupId is not the active board. The in-memory store
// enforces neither the foreign key nor a non-zero id, so these tests assert the stored GroupId
// directly instead of relying on the write failing. The same writes are proven against a real
// SQL Server in ShopItemSqlServerTests.
public class ShopManagementControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<FormUrlEncodedContent> BuildAntiForgeryFormAsync(
        HttpClient client, string getPath, Dictionary<string, string> formData)
    {
        var getResponse = await client.GetAsync(getPath, Token);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        return AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(formData, token);
    }

    // Reads every shop item regardless of board, so an item written to the wrong board (or to no
    // board at all) is still found and its GroupId can be checked. Test infrastructure only.
    private async Task<List<ShopItemEntity>> GetAllShopItemsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await context.ShopItems.IgnoreQueryFilters().AsNoTracking().ToListAsync(Token);
    }

    private static Dictionary<string, string> ItemForm(string name, ItemRarity rarity) => new()
    {
        ["Name"] = name,
        ["Description"] = "A throwaway item for the shop management tests",
        ["Type"] = ItemType.Equipment.ToString(),
        ["Rarity"] = rarity.ToString(),
        ["Price"] = "12",
        ["Quantity"] = "3"
    };

    [Theory]
    [InlineData(ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemRarity.Rare, ItemStatus.Draft)]
    public async Task Create_Post_DungeonMaster_StoresItemOnTheActiveBoard(ItemRarity rarity, ItemStatus expectedStatus)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"shop_dm_create_{rarity}", $"shop_dm_create_{rarity}@example.com", roles: ["DungeonMaster"]);
        var activeGroupId = factory.TestGroupContext.ActiveGroupId;
        activeGroupId.Should().NotBeNull();

        var form = await BuildAntiForgeryFormAsync(dmClient, "/ShopManagement/Create", ItemForm("Created Lantern", rarity));
        var response = await dmClient.PostAsync("/ShopManagement/Create", form, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var item = (await GetAllShopItemsAsync()).Should().ContainSingle(i => i.Name == "Created Lantern").Subject;
        item.GroupId.Should().Be(activeGroupId!.Value);
        item.CreatedByDmId.Should().Be(dm.Id);
        item.Status.Should().Be((int)expectedStatus);
    }

    [Fact]
    public async Task Create_Post_DungeonMaster_ItemIsListedOnTheManagementPage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "shop_dm_listed", "shop_dm_listed@example.com", roles: ["DungeonMaster"]);

        var form = await BuildAntiForgeryFormAsync(dmClient, "/ShopManagement/Create", ItemForm("Listed Compass", ItemRarity.Rare));
        (await dmClient.PostAsync("/ShopManagement/Create", form, Token)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var index = await dmClient.GetAsync("/ShopManagement", Token);
        index.StatusCode.Should().Be(HttpStatusCode.OK);
        (await index.Content.ReadAsStringAsync(Token)).Should().Contain("Listed Compass");
    }

    [Fact]
    public async Task Edit_Post_DungeonMaster_KeepsTheItemOnItsBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "shop_dm_edit", "shop_dm_edit@example.com", roles: ["DungeonMaster"]);
        var existing = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Edit Me");

        var formData = ItemForm("Edited Lantern", ItemRarity.Common);
        formData["Id"] = existing.Id.ToString();
        var form = await BuildAntiForgeryFormAsync(dmClient, $"/ShopManagement/Edit/{existing.Id}", formData);
        var response = await dmClient.PostAsync($"/ShopManagement/Edit/{existing.Id}", form, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var item = (await GetAllShopItemsAsync()).Should().ContainSingle().Subject;
        item.Name.Should().Be("Edited Lantern");
        item.GroupId.Should().Be(existing.GroupId);
        item.CreatedByDmId.Should().Be(dm.Id);
    }

    [Fact]
    public async Task Delete_Post_DungeonMaster_RemovesDraftItem()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "shop_dm_delete", "shop_dm_delete@example.com", roles: ["DungeonMaster"]);
        var existing = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Delete Me", rarity: ItemRarity.Rare);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == existing.Id, Token);
            tracked.Status = (int)ItemStatus.Draft;
            await context.SaveChangesAsync(Token);
        }

        var form = await BuildAntiForgeryFormAsync(dmClient, "/ShopManagement", []);
        var response = await dmClient.PostAsync($"/ShopManagement/Delete/{existing.Id}", form, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await GetAllShopItemsAsync()).Should().BeEmpty();
    }

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    // Posts a form that the server refuses (a reference link that is not a URL) and returns the page it shows again.
    private static async Task<string> PostRefusedFormAsync(HttpClient client, string path, Dictionary<string, string> formData, bool mobile)
    {
        formData["ReferenceUrl"] = "this is not a url";
        var form = await BuildAntiForgeryFormAsync(client, path, formData);
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        request.Headers.TryAddWithoutValidation("User-Agent", mobile ? MobileUserAgent : DesktopUserAgent);
        var response = await client.SendAsync(request, Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "a refused form is shown again, not redirected");
        return await response.Content.ReadAsStringAsync(Token);
    }

    private static string InputTag(string html, string attributeName, string attributeValue)
    {
        var match = Regex.Match(html, $@"<input\b[^>]*\b{attributeName}=""{attributeValue}""[^>]*>");
        match.Success.Should().BeTrue($"the page has an input with {attributeName}=\"{attributeValue}\"");
        return match.Value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_Post_RefusedWithAvailabilityDates_ShowsTheDatesInOpenFieldsWithTheBoxChecked(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"shop_dm_create_refused_{mobile}", $"shop_dm_create_refused_{mobile}@example.com", roles: ["DungeonMaster"]);
        var formData = ItemForm("Refused Lantern", ItemRarity.Rare);
        formData["AvailableFrom"] = "2030-01-05T10:00";
        formData["AvailableUntil"] = "2030-02-05T10:00";

        var html = await PostRefusedFormAsync(dmClient, "/ShopManagement/Create", formData, mobile);

        InputTag(html, "id", "enableAvailabilityWindow").Should().Contain("checked");
        InputTag(html, "name", "AvailableFrom").Should().Contain("2030-01-05T10:00").And.NotContain("disabled");
        InputTag(html, "name", "AvailableUntil").Should().Contain("2030-02-05T10:00").And.NotContain("disabled");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_Post_RefusedWithoutAvailabilityDates_KeepsTheBoxUncheckedAndTheFieldsDisabled(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"shop_dm_create_nodates_{mobile}", $"shop_dm_create_nodates_{mobile}@example.com", roles: ["DungeonMaster"]);

        var html = await PostRefusedFormAsync(dmClient, "/ShopManagement/Create", ItemForm("Refused Lantern", ItemRarity.Rare), mobile);

        InputTag(html, "id", "enableAvailabilityWindow").Should().NotContain("checked");
        InputTag(html, "name", "AvailableFrom").Should().Contain("disabled");
        InputTag(html, "name", "AvailableUntil").Should().Contain("disabled");
    }

    [Theory]
    [InlineData(false, ItemStatus.Published)]
    [InlineData(true, ItemStatus.Published)]
    [InlineData(false, ItemStatus.Archived)]
    [InlineData(true, ItemStatus.Archived)]
    public async Task Edit_Post_RefusedForANonDraftItem_ShowsTheItemsRealStatusAndNoUnderReviewNotice(bool mobile, ItemStatus status)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"shop_dm_edit_refused_{mobile}_{status}", $"shop_dm_edit_refused_{mobile}_{status}@example.com", roles: ["DungeonMaster"]);
        var existing = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Refused Edit", rarity: ItemRarity.Rare);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == existing.Id, Token);
            tracked.Status = (int)status;
            await context.SaveChangesAsync(Token);
        }
        var formData = ItemForm("Refused Edit", ItemRarity.Rare);
        formData["Id"] = existing.Id.ToString();

        var html = await PostRefusedFormAsync(dmClient, $"/ShopManagement/Edit/{existing.Id}", formData, mobile);

        html.Should().NotContain("Pending Review");
        html.Should().NotContain("awaiting DM approval");
        html.Should().NotContain("Item Under Review");
        html.Should().Contain(status == ItemStatus.Published ? "Available" : "Archived");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_Post_RefusedForADraftItem_StillShowsItAsAwaitingApproval(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"shop_dm_edit_draft_{mobile}", $"shop_dm_edit_draft_{mobile}@example.com", roles: ["DungeonMaster"]);
        var existing = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Refused Draft", rarity: ItemRarity.Rare);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == existing.Id, Token);
            tracked.Status = (int)ItemStatus.Draft;
            await context.SaveChangesAsync(Token);
        }
        var formData = ItemForm("Refused Draft", ItemRarity.Rare);
        formData["Id"] = existing.Id.ToString();

        var html = await PostRefusedFormAsync(dmClient, $"/ShopManagement/Edit/{existing.Id}", formData, mobile);

        html.Should().Contain("awaiting DM approval");
    }

    // ---- Archive and Reopen: only an item on sale can be taken off sale ---------------------------------

    private const string ArchiveRefusal = "Only published items can be archived.";

    private async Task<(HttpClient Client, UserEntity Viewer)> SeedViewerAsync(string handle, bool admin)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        return admin
            ? await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory, handle, $"{handle}@example.com")
            : await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, handle, $"{handle}@example.com");
    }

    private async Task<int> SeedItemAsync(int createdById, string name, ItemStatus status)
    {
        var item = await TestDataHelper.CreateShopItemAsync(factory.Services, createdById, name, rarity: ItemRarity.Rare);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == item.Id, Token);
        tracked.Status = (int)status;
        await context.SaveChangesAsync(Token);
        return item.Id;
    }

    private async Task<ItemStatus> ReadStatusAsync(int itemId)
    {
        var item = (await GetAllShopItemsAsync()).Should().ContainSingle(i => i.Id == itemId).Subject;
        return (ItemStatus)item.Status;
    }

    private static async Task<HttpResponseMessage> PostManagementActionAsync(HttpClient client, string action, int itemId)
    {
        var form = await BuildAntiForgeryFormAsync(client, "/ShopManagement", []);
        var response = await client.PostAsync($"/ShopManagement/{action}/{itemId}", form, Token);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, $"{action} answers with a redirect to the management list");
        response.Headers.Location!.OriginalString.Should().EndWith("/ShopManagement");
        return response;
    }

    [Theory]
    [InlineData(false, ItemStatus.Draft)]
    [InlineData(false, ItemStatus.Denied)]
    [InlineData(false, ItemStatus.Archived)]
    [InlineData(true, ItemStatus.Draft)]
    [InlineData(true, ItemStatus.Denied)]
    [InlineData(true, ItemStatus.Archived)]
    public async Task Archive_Post_AnItemThatIsNotPublished_IsRefusedAndKeepsItsStatus(bool admin, ItemStatus status)
    {
        var handle = $"shop_archive_refused_{(admin ? "admin" : "dm")}_{status}".ToLowerInvariant();
        var (client, viewer) = await SeedViewerAsync(handle, admin);
        var itemId = await SeedItemAsync(viewer.Id, "Not On Sale", status);

        var response = await PostManagementActionAsync(client, "Archive", itemId);

        var landing = await client.GetAsync(response.Headers.Location!.OriginalString, Token);
        landing.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await landing.Content.ReadAsStringAsync(Token)).Should().Contain(ArchiveRefusal);
        (await ReadStatusAsync(itemId)).Should().Be(status, "a refused archive changes nothing");
    }

    [Theory]
    [InlineData(false, ItemStatus.Draft)]
    [InlineData(false, ItemStatus.Denied)]
    [InlineData(true, ItemStatus.Draft)]
    [InlineData(true, ItemStatus.Denied)]
    public async Task ArchiveThenReopen_OnTheCreatorsOwnUnreviewedItem_NeverPublishesIt(bool admin, ItemStatus status)
    {
        var handle = $"shop_archive_reopen_{(admin ? "admin" : "dm")}_{status}".ToLowerInvariant();
        var (client, viewer) = await SeedViewerAsync(handle, admin);
        var itemId = await SeedItemAsync(viewer.Id, "Unreviewed", status);

        await PostManagementActionAsync(client, "Archive", itemId);
        await PostManagementActionAsync(client, "Reopen", itemId);

        (await ReadStatusAsync(itemId)).Should().Be(status, "an item no second Dungeon Master reviewed stays unpublished");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Archive_Post_APublishedItem_TakesItOffSale(bool admin, bool ownItem)
    {
        var handle = $"shop_archive_published_{(admin ? "admin" : "dm")}_{(ownItem ? "own" : "other")}";
        var (client, viewer) = await SeedViewerAsync(handle, admin);
        var ownerId = ownItem
            ? viewer.Id
            : (await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com")).Id;
        var itemId = await SeedItemAsync(ownerId, "On Sale", ItemStatus.Published);

        await PostManagementActionAsync(client, "Archive", itemId);

        (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Archived);
    }

    [Fact]
    public async Task Reopen_Post_AnItemArchivedFromSale_PutsItBackOnSale()
    {
        var (client, _) = await SeedViewerAsync("shop_reopen_archived", admin: false);
        var keeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "shop_reopen_archived_keeper", "shop_reopen_archived_keeper@example.com");
        var itemId = await SeedItemAsync(keeper.Id, "Back On Sale", ItemStatus.Published);

        await PostManagementActionAsync(client, "Archive", itemId);
        (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Archived);
        await PostManagementActionAsync(client, "Reopen", itemId);

        (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Published);
    }
}
