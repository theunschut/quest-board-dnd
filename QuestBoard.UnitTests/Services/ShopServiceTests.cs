using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class ShopServiceTests
{
    [Fact]
    public async Task GetPagedPublishedItemsAsync_DelegatesToRepository()
    {
        // Arrange
        var shopService = Substitute.For<IShopService>();

        var expectedItems = new List<ShopItem>
        {
            new() { Id = 1, Name = "Longsword" },
            new() { Id = 2, Name = "Magic Staff" }
        };

        shopService
            .GetPagedPublishedItemsAsync(
                Arg.Any<ItemType?>(),
                Arg.Any<IList<ItemRarity>?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns((expectedItems, 42));

        // Act
        var (Items, TotalCount) = await shopService.GetPagedPublishedItemsAsync(ItemType.Equipment, new List<ItemRarity> { ItemRarity.Rare }, "price_asc", "sword", 2, 12, TestContext.Current.CancellationToken);

        // Assert
        Items.Should().BeEquivalentTo(expectedItems);
        TotalCount.Should().Be(42);

        await shopService.Received(1).GetPagedPublishedItemsAsync(
            ItemType.Equipment,
            Arg.Is<IList<ItemRarity>>(r => r.Contains(ItemRarity.Rare)),
            "price_asc",
            "sword",
            2,
            12,
            Arg.Any<CancellationToken>());
    }
}
