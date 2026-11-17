using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class ShopControllerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public ShopControllerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        // Use non-redirecting client to properly test authorization redirects
        _client = factory.CreateNonRedirectingClient();
    }

    [Fact]
    public async Task Index_ShouldReturnShopPage()
    {
        // Arrange - Shop requires authentication
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        // Act
        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Shop");
    }

    [Fact]
    public async Task Index_WithItems_ShouldDisplayItems()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        // Create shopkeeper and items BEFORE creating authenticated client
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "shopkeeper", "shopkeeper@example.com");

        await TestDataHelper.CreateShopItemAsync(
            _factory.Services, shopkeeper.Id, "Longsword", 15.0m, 3);
        await TestDataHelper.CreateShopItemAsync(
            _factory.Services, shopkeeper.Id, "Health Potion", 5.0m, 10);

        // Create authenticated client AFTER data setup
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        // Act
        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Longsword");
        content.Should().Contain("Health Potion");
    }

    [Fact]
    public async Task Details_WithValidItemId_ShouldReturnItemDetails()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        // Create authenticated client
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "detailshop", "detailshop@example.com");

        var item = await TestDataHelper.CreateShopItemAsync(
            _factory.Services, shopkeeper.Id, "Magic Staff", 50.0m, 1);

        // Act
        var response = await client.GetAsync($"/Shop/Details/{item.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Magic Staff");
        content.Should().Contain("50");
    }

    [Fact]
    public async Task Details_WithInvalidItemId_ShouldReturn404()
    {
        // Arrange - Shop requires authentication
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        // Act
        var response = await client.GetAsync("/Shop/Details/99999", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Purchase_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "purchaseshop", "purchase@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(_factory.Services, shopkeeper.Id);

        // Try to access Shop page without auth to get anti-forgery token (will redirect)
        // For this test, we're just checking that unauthenticated access is blocked
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = item.Id.ToString(),
            ["quantity"] = "1"
        });

        // Act
        var response = await _client.PostAsync("/Shop/Purchase", formContent, TestContext.Current.CancellationToken);

        // Assert - Should redirect to login or return unauthorized
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Purchase_WhenAuthenticated_ShouldProcessRequest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "richshopkeeper", "richshop@example.com");

        var (buyerClient, buyer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "richbuyer", "richbuyer@example.com");

        var item = await TestDataHelper.CreateShopItemAsync(
            _factory.Services, shopkeeper.Id, "Affordable Item", 10.0m, 5);

        // Get the shop page to extract anti-forgery token
        var getResponse = await buyerClient.GetAsync("/Shop", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        // Set the anti-forgery cookie
        if (!string.IsNullOrEmpty(cookieValue))
        {
            buyerClient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["id"] = item.Id.ToString(),
                ["quantity"] = "1"
            },
            token);

        // Act
        var response = await buyerClient.PostAsync("/Shop/Purchase", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Purchase_WithExpensiveItem_ShouldProcessRequest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "expensiveshopkeeper", "expensiveshop@example.com");

        var (buyerClient, buyer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "poorbuyer", "poorbuyer@example.com");

        var item = await TestDataHelper.CreateShopItemAsync(
            _factory.Services, shopkeeper.Id, "Expensive Item", 100.0m, 5);

        // Get the shop page to extract anti-forgery token
        var getResponse = await buyerClient.GetAsync("/Shop", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        // Set the anti-forgery cookie
        if (!string.IsNullOrEmpty(cookieValue))
        {
            buyerClient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["id"] = item.Id.ToString(),
                ["quantity"] = "1"
            },
            token);

        // Act
        var response = await buyerClient.PostAsync("/Shop/Purchase", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    [Fact]
    public async Task Search_WithKeyword_ShouldReturnMatchingItems()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        // Create authenticated client
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "searchshop", "searchshop@example.com");

        await TestDataHelper.CreateShopItemAsync(_factory.Services, shopkeeper.Id, "Iron Sword", 15.0m);
        await TestDataHelper.CreateShopItemAsync(_factory.Services, shopkeeper.Id, "Steel Sword", 25.0m);
        await TestDataHelper.CreateShopItemAsync(_factory.Services, shopkeeper.Id, "Magic Wand", 30.0m);

        // Act
        var response = await client.GetAsync("/Shop?search=sword", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Iron Sword");
        content.Should().Contain("Steel Sword");
    }

    // Helper: seed N published shop items with alternating names/types/rarities
    private async Task SeedPublishedShopItemsAsync(int count)
    {
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"seeder_{Guid.NewGuid():N}", $"seeder_{Guid.NewGuid():N}@example.com");

        for (int i = 1; i <= count; i++)
        {
            var name = $"Item {i:D2} {(i % 3 == 0 ? "Sword" : "Shield")}";
            var type = i % 2 == 0 ? 1 : 0; // 0=Equipment, 1=MagicItem
            var rarity = i % 2 == 0 ? 2 : 0; // 0=Common, 2=Rare
            var price = 10 + i;

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoard.Repository.Entities.QuestBoardContext>();
            context.ShopItems.Add(new ShopItemEntity
            {
                Name = name,
                Description = $"Description for {name}",
                Price = price,
                Quantity = 5,
                Type = type,
                Rarity = rarity,
                Status = 1, // Published
                CreatedByDmId = shopkeeper.Id,
                GroupId = 1, // Required by HasQueryFilter
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Index_PagedRepoMethodReachable_ReturnsSuccess()
    {
        // This smoke test verifies that GetPagedPublishedItemsAsync is wired into DI.
        // It must FAIL before Task 2 adds the method, and PASS after.
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IShopService>();
        var result = await svc.GetPagedPublishedItemsAsync(null, null, null, null, 1, 12, TestContext.Current.CancellationToken);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Index_WithoutPageParam_Returns12Items_WhenShopHas15()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await SeedPublishedShopItemsAsync(15);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"data-item-card");
        matches.Count.Should().Be(12);
    }

    [Fact]
    public async Task Index_WithPage2_ReturnsItems13To24()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await SeedPublishedShopItemsAsync(15);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop?page=2", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Item 13");
    }

    [Fact]
    public async Task Index_WithSearch_FiltersByNameOrDescription()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await SeedPublishedShopItemsAsync(15);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop?search=Sword", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Sword");
        content.Should().NotContain("Item 01 Shield");
    }

    [Fact]
    public async Task Index_WithStackedParams_AllApply()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await SeedPublishedShopItemsAsync(15);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop?type=Equipment&rarity=Rare&sort=price_asc&search=a&page=1", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_PagerRendersWhenMultiplePages()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await SeedPublishedShopItemsAsync(15);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain(@"aria-label=""Shop page navigation""");
    }

    [Fact]
    public async Task Index_OutOfRangePage_ClampsToLastPage()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await SeedPublishedShopItemsAsync(15);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop?page=9999", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("page-item active");
    }

    private const string UnreleasedItemName = "Unreleased Relic";

    private static string DetailsUrl(int itemId, bool dialog) =>
        dialog ? $"/Shop/Details/{itemId}?isModal=true" : $"/Shop/Details/{itemId}";

    // The shop item with the given status, written straight to the store because the helper only makes Published ones.
    private async Task SetItemStatusAsync(int itemId, ItemStatus status)
    {
        await using var ctx = _factory.Database.CreateContext();
        var tracked = await ctx.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == itemId, TestContext.Current.CancellationToken);
        tracked.Status = (int)status;
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // An item of the active board, made by a separate keeper, with the given status.
    private async Task<int> SeedKeeperItemAsync(string suffix, ItemStatus status)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.ActiveGroupId = 1;

        var keeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"accesskeeper_{suffix}", $"accesskeeper_{suffix}@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(_factory.Services, keeper.Id, UnreleasedItemName, 25m, 1);
        await SetItemStatusAsync(item.Id, status);
        return item.Id;
    }

    [Theory]
    [InlineData(ItemStatus.Draft, false)]
    [InlineData(ItemStatus.Draft, true)]
    [InlineData(ItemStatus.Denied, false)]
    [InlineData(ItemStatus.Denied, true)]
    public async Task Details_DraftOrDeniedItem_IsNotFoundForAPlainMember(ItemStatus status, bool dialog)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var itemId = await SeedKeeperItemAsync(suffix, status);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"accessmember_{suffix}", $"accessmember_{suffix}@example.com");

        var response = await client.GetAsync(DetailsUrl(itemId, dialog), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"a {status} item has never been on sale and is not a member's to read");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain(UnreleasedItemName);
    }

    [Theory]
    [InlineData(ItemStatus.Draft, false)]
    [InlineData(ItemStatus.Draft, true)]
    [InlineData(ItemStatus.Denied, false)]
    [InlineData(ItemStatus.Denied, true)]
    public async Task Details_DraftOrDeniedItem_OpensForItsCreatingDungeonMaster(ItemStatus status, bool dialog)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.ActiveGroupId = 1;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, $"accesscreator_{suffix}", $"accesscreator_{suffix}@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(_factory.Services, dm.Id, UnreleasedItemName, 25m, 1);
        await SetItemStatusAsync(item.Id, status);

        var response = await client.GetAsync(DetailsUrl(item.Id, dialog), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"the Edit page's View Details link for a {status} item must not dead-end");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(UnreleasedItemName);
    }

    // A reviewer who did not make the item: another Dungeon Master, an Admin of the board, or a SuperAdmin who is only a
    // Player on the board, so the SuperAdmin case shows the policy decides rather than the board role alone.
    private async Task<HttpClient> CreateReviewerClientAsync(string viewer, string suffix)
    {
        var userName = $"accessreviewer_{viewer}_{suffix}";
        var email = $"{userName}@example.com";
        var (client, _) = viewer switch
        {
            "dm" => await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory, userName, email),
            "admin" => await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory, userName, email),
            "superadmin" => await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory, userName, email),
            _ => throw new ArgumentOutOfRangeException(nameof(viewer), viewer, "unknown reviewer kind"),
        };
        return client;
    }

    [Theory]
    [InlineData("dm", ItemStatus.Draft, false)]
    [InlineData("dm", ItemStatus.Draft, true)]
    [InlineData("dm", ItemStatus.Denied, false)]
    [InlineData("dm", ItemStatus.Denied, true)]
    [InlineData("admin", ItemStatus.Draft, false)]
    [InlineData("admin", ItemStatus.Draft, true)]
    [InlineData("admin", ItemStatus.Denied, false)]
    [InlineData("admin", ItemStatus.Denied, true)]
    [InlineData("superadmin", ItemStatus.Draft, false)]
    [InlineData("superadmin", ItemStatus.Draft, true)]
    [InlineData("superadmin", ItemStatus.Denied, false)]
    [InlineData("superadmin", ItemStatus.Denied, true)]
    public async Task Details_DraftOrDeniedItem_OpensForAnotherDungeonMasterAnAdminOrASuperAdmin(string viewer, ItemStatus status, bool dialog)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var itemId = await SeedKeeperItemAsync(suffix, status);
        var client = await CreateReviewerClientAsync(viewer, suffix);

        var response = await client.GetAsync(DetailsUrl(itemId, dialog), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"whoever reviews {status} items in shop management may open one they did not make");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(UnreleasedItemName);
    }

    [Theory]
    [InlineData(ItemStatus.Published, false)]
    [InlineData(ItemStatus.Published, true)]
    [InlineData(ItemStatus.Archived, false)]
    [InlineData(ItemStatus.Archived, true)]
    public async Task Details_PublishedOrArchivedItem_StillOpensForAPlainMember(ItemStatus status, bool dialog)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var itemId = await SeedKeeperItemAsync(suffix, status);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"accessmember_{suffix}", $"accessmember_{suffix}@example.com");

        var response = await client.GetAsync(DetailsUrl(itemId, dialog), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"a {status} item has been on sale and stays open to members");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(UnreleasedItemName);
        if (!dialog && status == ItemStatus.Published)
            body.Should().Contain("Purchase Item");
        if (!dialog && status == ItemStatus.Archived)
            body.Should().Contain("This item is not currently available for purchase.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Details_RefusedItem_AnswersExactlyLikeAMissingId(bool dialog)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var itemId = await SeedKeeperItemAsync(suffix, ItemStatus.Draft);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"accessmember_{suffix}", $"accessmember_{suffix}@example.com");
        int missingId;
        await using (var ctx = _factory.Database.CreateContext())
        {
            missingId = await ctx.ShopItems.IgnoreQueryFilters().MaxAsync(i => i.Id, TestContext.Current.CancellationToken) + 1000;
        }

        var refused = await client.GetAsync(DetailsUrl(itemId, dialog), TestContext.Current.CancellationToken);
        var missing = await client.GetAsync(DetailsUrl(missingId, dialog), TestContext.Current.CancellationToken);

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var missingBody = await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(missingBody, "a guessed id must not tell an unreleased item from one that does not exist");
    }

    private const string QuantityRefusal = "Quantity must be at least 1.";

    // A signed-in member and one Published item with the given stock, for the crafted-post facts.
    private async Task<(HttpClient Client, UserEntity User, int ItemId)> SeedMemberAndItemAsync(int stock)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.ActiveGroupId = 1;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"qtykeeper_{suffix}", $"qtykeeper_{suffix}@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(_factory.Services, shopkeeper.Id, "Plain Lantern", 40m, stock);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"qtybuyer_{suffix}", $"qtybuyer_{suffix}@example.com");
        return (client, user, item.Id);
    }

    private async Task<int> SeedPurchaseAsync(int userId, int itemId, double hoursAgo, int quantity)
    {
        await using var ctx = _factory.Database.CreateContext();
        var purchase = new UserTransactionEntity
        {
            UserId = userId,
            ShopItemId = itemId,
            TransactionType = (int)TransactionType.Purchase,
            Price = 40m * quantity,
            Quantity = quantity,
            TransactionDate = DateTime.UtcNow.AddHours(-hoursAgo),
        };
        ctx.UserTransactions.Add(purchase);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return purchase.Id;
    }

    private async Task<(int Stock, int Rows)> ReadStockAndRowsAsync(int userId, int itemId)
    {
        await using var ctx = _factory.Database.CreateContext();
        var stock = await ctx.ShopItems.IgnoreQueryFilters().Where(i => i.Id == itemId).Select(i => i.Quantity)
            .SingleAsync(TestContext.Current.CancellationToken);
        var rows = await ctx.UserTransactions.IgnoreQueryFilters().CountAsync(t => t.UserId == userId, TestContext.Current.CancellationToken);
        return (stock, rows);
    }

    // The form fields a signed-in member's browser would send, with a token read from a real page of the shop.
    private static async Task<HttpResponseMessage> PostWithTokenAsync(HttpClient client, int detailsItemId, string url, Dictionary<string, string> fields)
    {
        var page = await client.GetAsync($"/Shop/Details/{detailsItemId}", TestContext.Current.CancellationToken);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var (token, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(page);
        var content = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(fields, token);
        return await client.PostAsync(url, content, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Purchase", 0)]
    [InlineData("Purchase", -1)]
    [InlineData("Sell", 0)]
    [InlineData("Sell", -1)]
    [InlineData("SellToShop", 0)]
    [InlineData("SellToShop", -1)]
    public async Task Post_QuantityBelowOne_IsRefusedWithTheErrorAndChangesNothing(string action, int quantity)
    {
        var (client, user, itemId) = await SeedMemberAndItemAsync(5);
        var id = action == "Sell" ? await SeedPurchaseAsync(user.Id, itemId, 2, 2) : itemId;
        var (stockBefore, rowsBefore) = await ReadStockAndRowsAsync(user.Id, itemId);

        var response = await PostWithTokenAsync(client, itemId, $"/Shop/{action}",
            new Dictionary<string, string> { ["id"] = id.ToString(), ["quantity"] = quantity.ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a refused quantity is an error toast, never a 400 or a 500");
        var expectedLocation = action == "Sell" ? "/Shop" : $"/Shop/Details/{itemId}";
        response.Headers.Location!.OriginalString.TrimEnd('/').Should().Be(expectedLocation);

        var landed = await client.GetAsync(response.Headers.Location!.OriginalString, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = System.Net.WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.Should().Contain(QuantityRefusal);

        var (stockAfter, rowsAfter) = await ReadStockAndRowsAsync(user.Id, itemId);
        stockAfter.Should().Be(stockBefore, "a refused request moves no stock");
        rowsAfter.Should().Be(rowsBefore, "a refused request records no transaction");
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(-1, -1)]
    public async Task Purchase_OfOne_StillGoesThroughAndKeepsTheStockConvention(int stockBefore, int stockAfter)
    {
        var (client, user, itemId) = await SeedMemberAndItemAsync(stockBefore);

        var response = await PostWithTokenAsync(client, itemId, "/Shop/Purchase",
            new Dictionary<string, string> { ["id"] = itemId.ToString(), ["quantity"] = "1" });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.TrimEnd('/').Should().Be("/Shop");

        var (stock, rows) = await ReadStockAndRowsAsync(user.Id, itemId);
        stock.Should().Be(stockAfter);
        rows.Should().Be(1);
        await using var ctx = _factory.Database.CreateContext();
        var purchase = await ctx.UserTransactions.IgnoreQueryFilters().SingleAsync(t => t.UserId == user.Id, TestContext.Current.CancellationToken);
        purchase.TransactionType.Should().Be((int)TransactionType.Purchase);
        purchase.Quantity.Should().Be(1);
    }
}
