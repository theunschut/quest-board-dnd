using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class ShopSeedServiceTests
{
    [Fact]
    public async Task SeedBasicEquipmentAsync_NoExistingItems_SetsGroupIdOnEverySeededItem()
    {
        // Arrange
        var shopService = Substitute.For<IShopService>();
        shopService.GetPublishedItemsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ShopItem>());

        var addedItems = new List<ShopItem>();
        shopService.When(x => x.AddAsync(Arg.Any<ShopItem>(), Arg.Any<CancellationToken>()))
            .Do(x => addedItems.Add(x.Arg<ShopItem>()));

        var sut = new ShopSeedService(shopService);

        // Act
        await sut.SeedBasicEquipmentAsync(createdByUserId: 5, groupId: 3);

        // Assert
        addedItems.Should().NotBeEmpty();
        addedItems.Should().OnlyContain(i => i.GroupId == 3);
        addedItems.Should().OnlyContain(i => i.CreatedByDmId == 5);
    }

    [Fact]
    public async Task SeedBasicEquipmentAsync_GroupAlreadyHasPublishedItems_DoesNotReseed()
    {
        // Arrange
        var shopService = Substitute.For<IShopService>();
        shopService.GetPublishedItemsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ShopItem> { new() { Id = 1, GroupId = 3 } });

        var sut = new ShopSeedService(shopService);

        // Act
        await sut.SeedBasicEquipmentAsync(createdByUserId: 5, groupId: 3);

        // Assert
        await shopService.DidNotReceive().AddAsync(Arg.Any<ShopItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedBasicEquipmentAsync_OtherGroupAlreadyHasPublishedItems_StillSeedsThisGroup()
    {
        // Arrange
        var shopService = Substitute.For<IShopService>();
        shopService.GetPublishedItemsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ShopItem> { new() { Id = 1, GroupId = 99 } });

        var sut = new ShopSeedService(shopService);

        // Act
        await sut.SeedBasicEquipmentAsync(createdByUserId: 5, groupId: 3);

        // Assert
        await shopService.Received().AddAsync(Arg.Is<ShopItem>(i => i.GroupId == 3), Arg.Any<CancellationToken>());
    }
}
