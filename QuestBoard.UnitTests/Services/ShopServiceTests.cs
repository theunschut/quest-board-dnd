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

    [Fact]
    public async Task SellItemToShopAsync_QuantityThatWouldOverflowTheStock_IsRefusedAndChangesNothing()
    {
        var (service, shop, transactions) = CreateService();
        var item = new ShopItem { Id = 1, Name = "Rope", Price = 40m, Quantity = 2, Status = ItemStatus.Published };
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(item);

        var act = () => service.SellItemToShopAsync(1, int.MaxValue, Member, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        item.Quantity.Should().Be(2);
        await shop.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await transactions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task SellItemToShopAsync_QuantityThatFillsTheStockExactlyToTheLimit_IsAccepted()
    {
        var (service, shop, transactions) = CreateService();
        var item = new ShopItem { Id = 1, Name = "Rope", Price = 40m, Quantity = 2, Status = ItemStatus.Published };
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(item);

        await service.SellItemToShopAsync(1, int.MaxValue - 2, Member, TestContext.Current.CancellationToken);

        item.Quantity.Should().Be(int.MaxValue);
        await transactions.Received(1).AddAsync(Arg.Any<UserTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SellItemToShopAsync_LargeQuantityOfAnUnlimitedItem_LeavesStockAtMinusOne()
    {
        var (service, shop, transactions) = CreateService();
        var item = new ShopItem { Id = 1, Name = "Rope", Price = 40m, Quantity = -1, Status = ItemStatus.Published };
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(item);

        await service.SellItemToShopAsync(1, int.MaxValue, Member, TestContext.Current.CancellationToken);

        item.Quantity.Should().Be(-1);
        await transactions.Received(1).AddAsync(Arg.Any<UserTransaction>(), Arg.Any<CancellationToken>());
    }

    // The return window the history list offers a Return by and the one the service decides a posted return by are one
    // definition, so for any purchase age the list's answer and the service's outcome agree.
    [Theory]
    [InlineData(1, true)]
    [InlineData(23.5, true)]
    [InlineData(24.5, false)]
    [InlineData(48, false)]
    public async Task ReturnOrSellItemAsync_FollowsTheSameWindowTheHistoryListOffersAReturnBy(double hoursAgo, bool expectedReturn)
    {
        var (service, shop, transactions) = CreateService();
        var purchase = new UserTransaction
        {
            Id = 5,
            ShopItemId = 1,
            UserId = Member.Id,
            Quantity = 2,
            Price = 200m,
            TransactionType = TransactionType.Purchase,
            TransactionDate = DateTime.UtcNow.AddHours(-hoursAgo),
        };
        transactions.GetTransactionWithDetailsAsync(5, Arg.Any<CancellationToken>()).Returns(purchase);
        transactions.GetTransactionsByUserAsync(Member.Id, Arg.Any<CancellationToken>()).Returns(new List<UserTransaction> { purchase });
        shop.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 1, Name = "Rope", Price = 100m, Quantity = -1, Status = ItemStatus.Published });

        var result = await service.ReturnOrSellItemAsync(5, 2, Member, TestContext.Current.CancellationToken);

        (result.Price == 200m).Should().Be(expectedReturn, "a return pays full price and a sell-back pays half");
        var viewModel = new QuestBoard.Service.ViewModels.ShopViewModels.UserTransactionViewModel { TransactionDate = purchase.TransactionDate };
        viewModel.CanReturn.Should().Be(expectedReturn);
    }
}
