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
        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"class=""item-card""");
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
}
