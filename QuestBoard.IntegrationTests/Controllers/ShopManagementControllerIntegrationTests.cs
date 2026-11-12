using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;

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
}
