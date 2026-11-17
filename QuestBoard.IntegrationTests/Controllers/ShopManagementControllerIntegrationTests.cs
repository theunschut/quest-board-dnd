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
    [InlineData(ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemRarity.Legendary, ItemStatus.Draft)]
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

    private async Task<int> SeedItemAsync(int createdById, string name, ItemStatus status, ItemRarity rarity = ItemRarity.Rare)
    {
        var item = await TestDataHelper.CreateShopItemAsync(factory.Services, createdById, name, rarity: rarity);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == item.Id, Token);
        tracked.Status = (int)status;
        if (status == ItemStatus.Denied)
        {
            tracked.DenialReason = "Too strong for this board";
            tracked.DeniedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        }
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

    // ---- Edit: an edit never publishes, and an item raised into a rarity that needs review goes back to review --

    public enum Viewer
    {
        DungeonMaster,
        Admin,
        SuperAdmin
    }

    private const string ResubmittedToast = "Item updated and resubmitted for review!";
    private const string SentBackToast = "Item updated and sent back for review. It is off sale until another Dungeon Master publishes it.";
    private const string UpdatedToast = "Item updated successfully!";

    private async Task<(HttpClient Client, UserEntity Viewer)> SeedViewerAsync(string handle, Viewer viewer)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        return viewer switch
        {
            Viewer.Admin => await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory, handle, $"{handle}@example.com"),
            Viewer.SuperAdmin => await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory, handle, $"{handle}@example.com"),
            _ => await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, handle, $"{handle}@example.com")
        };
    }

    // Posts the Edit form for an item, follows the redirect to the management list and returns that page decoded.
    private async Task<string> PostEditAsync(HttpClient client, int itemId, string newName, ItemRarity newRarity)
    {
        var formData = ItemForm(newName, newRarity);
        formData["Id"] = itemId.ToString();
        var form = await BuildAntiForgeryFormAsync(client, $"/ShopManagement/Edit/{itemId}", formData);
        var response = await client.PostAsync($"/ShopManagement/Edit/{itemId}", form, Token);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a saved edit answers with a redirect to the management list");
        var location = response.Headers.Location!.OriginalString;
        location.Should().EndWith("/ShopManagement");

        var landing = await client.GetAsync(location, Token);
        landing.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await landing.Content.ReadAsStringAsync(Token));
    }

    private async Task<ShopItemEntity> ReadItemAsync(int itemId) =>
        (await GetAllShopItemsAsync()).Should().ContainSingle(i => i.Id == itemId).Subject;

    [Theory]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Rare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Rare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Archived)]
    public async Task Edit_Post_ByTheItemsCreator_LeavesTheStatusTheReviewRuleGives(
        ItemStatus fromStatus, ItemRarity storedRarity, ItemRarity newRarity, ItemStatus expectedStatus)
    {
        var handle = $"shop_edit_creator_{fromStatus}_{storedRarity}_{newRarity}".ToLowerInvariant();
        var (client, viewer) = await SeedViewerAsync(handle, Viewer.DungeonMaster);
        var itemId = await SeedItemAsync(viewer.Id, "Before Edit", fromStatus, storedRarity);

        var landing = await PostEditAsync(client, itemId, "After Edit", newRarity);

        var item = await ReadItemAsync(itemId);
        ((ItemStatus)item.Status).Should().Be(expectedStatus);
        item.Name.Should().Be("After Edit");
        item.Rarity.Should().Be((int)newRarity);
        if (fromStatus == ItemStatus.Denied)
        {
            item.DenialReason.Should().BeNull("a resubmitted item carries no old denial");
            item.DeniedAt.Should().BeNull();
        }

        var expectedToast = expectedStatus == fromStatus && expectedStatus != ItemStatus.Draft
            ? UpdatedToast
            : fromStatus is ItemStatus.Draft or ItemStatus.Denied ? ResubmittedToast : SentBackToast;
        landing.Should().Contain(expectedToast);
        if (expectedStatus == ItemStatus.Draft)
        {
            landing.Should().NotContain(UpdatedToast, "an item waiting for review was not simply updated");
        }
    }

    [Theory]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Draft, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Denied, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Published, ItemRarity.Common, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Published, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Published, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Published)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Archived, ItemRarity.Common, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Archived, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Archived)]
    [InlineData(Viewer.DungeonMaster, ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(Viewer.Admin, ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(Viewer.Admin, ItemStatus.Draft, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.Admin, ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(Viewer.Admin, ItemStatus.Denied, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.Admin, ItemStatus.Published, ItemRarity.Common, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(Viewer.Admin, ItemStatus.Published, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.Admin, ItemStatus.Published, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Published)]
    [InlineData(Viewer.Admin, ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(Viewer.Admin, ItemStatus.Archived, ItemRarity.Common, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(Viewer.Admin, ItemStatus.Archived, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.Admin, ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Archived)]
    [InlineData(Viewer.Admin, ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Draft, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Denied, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Published, ItemRarity.Common, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Published, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Published, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Published)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Archived, ItemRarity.Common, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Archived, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Archived)]
    [InlineData(Viewer.SuperAdmin, ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Archived)]
    public async Task Edit_Post_OnAnotherKeepersItem_IsSavedButNeverPublishedByTheEdit(
        Viewer viewer, ItemStatus fromStatus, ItemRarity storedRarity, ItemRarity newRarity, ItemStatus expectedStatus)
    {
        var handle = $"shop_edit_other_{viewer}_{fromStatus}_{storedRarity}_{newRarity}".ToLowerInvariant();
        var (client, _) = await SeedViewerAsync(handle, viewer);
        var keeper = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com");
        var itemId = await SeedItemAsync(keeper.Id, "Keepers Item", fromStatus, storedRarity);

        await PostEditAsync(client, itemId, "Edited By Another", newRarity);

        var item = await ReadItemAsync(itemId);
        ((ItemStatus)item.Status).Should().Be(expectedStatus);
        item.Name.Should().Be("Edited By Another");
        item.CreatedByDmId.Should().Be(keeper.Id, "an edit does not change who created the item");
    }

    public enum EditChain
    {
        DraftToCommonThenToLegendary,
        PublishedCommonToLegendary,
        PublishedUncommonToRare,
        ArchivedCommonToVeryRare
    }

    [Theory]
    [InlineData(Viewer.DungeonMaster, EditChain.DraftToCommonThenToLegendary)]
    [InlineData(Viewer.DungeonMaster, EditChain.PublishedCommonToLegendary)]
    [InlineData(Viewer.DungeonMaster, EditChain.PublishedUncommonToRare)]
    [InlineData(Viewer.DungeonMaster, EditChain.ArchivedCommonToVeryRare)]
    [InlineData(Viewer.Admin, EditChain.DraftToCommonThenToLegendary)]
    [InlineData(Viewer.Admin, EditChain.PublishedCommonToLegendary)]
    [InlineData(Viewer.Admin, EditChain.PublishedUncommonToRare)]
    [InlineData(Viewer.Admin, EditChain.ArchivedCommonToVeryRare)]
    [InlineData(Viewer.SuperAdmin, EditChain.DraftToCommonThenToLegendary)]
    [InlineData(Viewer.SuperAdmin, EditChain.PublishedCommonToLegendary)]
    [InlineData(Viewer.SuperAdmin, EditChain.PublishedUncommonToRare)]
    [InlineData(Viewer.SuperAdmin, EditChain.ArchivedCommonToVeryRare)]
    public async Task EditThenPublish_ByTheItemsCreator_NeverPutsAnItemRaisedIntoReviewOnSale(Viewer viewer, EditChain chain)
    {
        var handle = $"shop_edit_chain_{viewer}_{chain}".ToLowerInvariant();
        var (client, creator) = await SeedViewerAsync(handle, viewer);
        int itemId;
        if (chain == EditChain.DraftToCommonThenToLegendary)
        {
            itemId = await SeedItemAsync(creator.Id, "Chained Item", ItemStatus.Draft, ItemRarity.Rare);
            await PostEditAsync(client, itemId, "Chained Item", ItemRarity.Common);
            (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Draft, "an edit to a review-free rarity does not publish a draft");
            await PostEditAsync(client, itemId, "Chained Item", ItemRarity.Legendary);
        }
        else
        {
            var (fromStatus, fromRarity, toRarity) = chain switch
            {
                EditChain.PublishedCommonToLegendary => (ItemStatus.Published, ItemRarity.Common, ItemRarity.Legendary),
                EditChain.PublishedUncommonToRare => (ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Rare),
                _ => (ItemStatus.Archived, ItemRarity.Common, ItemRarity.VeryRare)
            };
            itemId = await SeedItemAsync(creator.Id, "Chained Item", fromStatus, fromRarity);
            await PostEditAsync(client, itemId, "Chained Item", toRarity);
        }
        (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Draft, "an item raised into a rarity that needs review waits for review");

        // The creator's own Publish is refused as it always has been; only what it leaves behind matters here.
        var ownForm = await BuildAntiForgeryFormAsync(client, "/ShopManagement", []);
        await client.PostAsync($"/ShopManagement/Publish/{itemId}", ownForm, Token);
        (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Draft, "the creator cannot approve their own edited item");

        var (reviewer, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, $"{handle}_reviewer", $"{handle}_reviewer@example.com");
        await PostManagementActionAsync(reviewer, "Publish", itemId);
        (await ReadStatusAsync(itemId)).Should().Be(ItemStatus.Published, "another Dungeon Master's review still puts it on sale");
    }

    // ---- Delete: a Dungeon Master removes only their own unpublished work; Admins remove anything -------------

    private const string DeletedToast = "Item deleted successfully!";
    private const string PublishedRefusal = "Cannot delete items that have been published.";
    private const string NotYoursRefusal = "You can only delete your own items.";

    [Theory]
    [InlineData(Viewer.DungeonMaster, true, ItemStatus.Draft, true, DeletedToast)]
    [InlineData(Viewer.DungeonMaster, true, ItemStatus.Denied, true, DeletedToast)]
    [InlineData(Viewer.DungeonMaster, true, ItemStatus.Published, false, PublishedRefusal)]
    [InlineData(Viewer.DungeonMaster, true, ItemStatus.Archived, false, PublishedRefusal)]
    [InlineData(Viewer.DungeonMaster, false, ItemStatus.Draft, false, NotYoursRefusal)]
    [InlineData(Viewer.DungeonMaster, false, ItemStatus.Denied, false, NotYoursRefusal)]
    [InlineData(Viewer.DungeonMaster, false, ItemStatus.Published, false, NotYoursRefusal)]
    [InlineData(Viewer.DungeonMaster, false, ItemStatus.Archived, false, NotYoursRefusal)]
    [InlineData(Viewer.Admin, true, ItemStatus.Draft, true, DeletedToast)]
    [InlineData(Viewer.Admin, true, ItemStatus.Denied, true, DeletedToast)]
    [InlineData(Viewer.Admin, true, ItemStatus.Published, true, DeletedToast)]
    [InlineData(Viewer.Admin, true, ItemStatus.Archived, true, DeletedToast)]
    [InlineData(Viewer.Admin, false, ItemStatus.Draft, true, DeletedToast)]
    [InlineData(Viewer.Admin, false, ItemStatus.Denied, true, DeletedToast)]
    [InlineData(Viewer.Admin, false, ItemStatus.Published, true, DeletedToast)]
    [InlineData(Viewer.Admin, false, ItemStatus.Archived, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, true, ItemStatus.Draft, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, true, ItemStatus.Denied, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, true, ItemStatus.Published, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, true, ItemStatus.Archived, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, false, ItemStatus.Draft, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, false, ItemStatus.Denied, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, false, ItemStatus.Published, true, DeletedToast)]
    [InlineData(Viewer.SuperAdmin, false, ItemStatus.Archived, true, DeletedToast)]
    public async Task Delete_Post_FollowsWhoCreatedTheItemAndWhetherItHasBeenOnSale(
        Viewer viewer, bool ownItem, ItemStatus status, bool expectedDeleted, string expectedToast)
    {
        var handle = $"shop_delete_{viewer}_{(ownItem ? "own" : "other")}_{status}".ToLowerInvariant();
        var (client, viewerUser) = await SeedViewerAsync(handle, viewer);
        var creatorId = ownItem
            ? viewerUser.Id
            : (await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com")).Id;
        var itemId = await SeedItemAsync(creatorId, "Delete Candidate", status);

        var response = await PostManagementActionAsync(client, "Delete", itemId);

        var landing = await client.GetAsync(response.Headers.Location!.OriginalString, Token);
        landing.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await landing.Content.ReadAsStringAsync(Token)).Should().Contain(expectedToast);
        var stored = (await GetAllShopItemsAsync()).Where(i => i.Id == itemId).ToList();
        if (expectedDeleted)
        {
            stored.Should().BeEmpty();
        }
        else
        {
            ((ItemStatus)stored.Should().ContainSingle().Subject.Status).Should().Be(status, "a refused delete changes nothing");
        }
    }
}
