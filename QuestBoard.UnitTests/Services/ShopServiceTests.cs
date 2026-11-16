using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using AutoMapper;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Domain.Services;
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

    private const string QuantityRefusal = "Quantity must be at least 1.";

    private static (ShopService Service, IShopRepository Shop, IUserTransactionRepository Transactions) CreateService()
    {
        var shop = Substitute.For<IShopRepository>();
        var transactions = Substitute.For<IUserTransactionRepository>();
        var service = new ShopService(shop, transactions, Substitute.For<IMapper>());
        return (service, shop, transactions);
    }

    private static readonly User Member = new() { Id = 7, Name = "Member" };

    private static async Task AssertNothingReadOrWrittenAsync(IShopRepository shop, IUserTransactionRepository transactions)
    {
        await shop.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        await shop.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await transactions.DidNotReceiveWithAnyArgs().GetTransactionWithDetailsAsync(default, default);
        await transactions.DidNotReceiveWithAnyArgs().GetTransactionsByUserAsync(default, default);
        await transactions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PurchaseItemAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall(int quantity)
    {
        var (service, shop, transactions) = CreateService();

        var act = () => service.PurchaseItemAsync(1, quantity, Member, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(QuantityRefusal);
        await AssertNothingReadOrWrittenAsync(shop, transactions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ReturnOrSellItemAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall(int quantity)
    {
        var (service, shop, transactions) = CreateService();

        var act = () => service.ReturnOrSellItemAsync(1, quantity, Member, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(QuantityRefusal);
        await AssertNothingReadOrWrittenAsync(shop, transactions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SellItemToShopAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall(int quantity)
    {
        var (service, shop, transactions) = CreateService();

        var act = () => service.SellItemToShopAsync(1, quantity, Member, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(QuantityRefusal);
        await AssertNothingReadOrWrittenAsync(shop, transactions);
    }

    [Fact]
    public async Task PurchaseItemAsync_OneOfAnUnlimitedItem_LeavesStockAtMinusOneAndRecordsThePurchase()
    {
        var (service, shop, transactions) = CreateService();
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 1, Name = "Rope", Price = 40m, Quantity = -1, Status = ItemStatus.Published });

        var result = await service.PurchaseItemAsync(1, 1, Member, TestContext.Current.CancellationToken);

        result.Quantity.Should().Be(1);
        result.Price.Should().Be(40m);
        await shop.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await transactions.Received(1).AddAsync(Arg.Any<UserTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchaseItemAsync_OneOfALimitedItem_TakesOneFromStock()
    {
        var (service, shop, transactions) = CreateService();
        var item = new ShopItem { Id = 1, Name = "Rope", Price = 40m, Quantity = 3, Status = ItemStatus.Published };
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(item);

        await service.PurchaseItemAsync(1, 1, Member, TestContext.Current.CancellationToken);

        item.Quantity.Should().Be(2);
        await shop.Received(1).UpdateAsync(item, Arg.Any<CancellationToken>());
        await transactions.Received(1).AddAsync(Arg.Any<UserTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchaseItemAsync_OfASoldOutItem_IsStillRefusedAsSoldOut()
    {
        var (service, shop, transactions) = CreateService();
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 1, Name = "Rope", Price = 40m, Quantity = 0, Status = ItemStatus.Published });

        var act = () => service.PurchaseItemAsync(1, 1, Member, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("This item is sold out.");
        await transactions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
