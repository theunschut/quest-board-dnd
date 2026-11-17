using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Services;

// Runs the shop service over the real repositories and entity map on one in-memory context per request, the way a
// request scope does, so a stock write is judged against whatever the same request already read and tracked.
public class ShopServiceStockWriteTests
{
    private const int BoardId = 1;
    private const int BuyerId = 700;
    private const int KeeperId = 701;
    private const int ItemId = 70;
    private const int PurchaseId = 71;
    private const string KeeperName = "Keeper";
    private const string KeeperEmail = "keeper@test.com";

    private static QuestBoardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new BoardOneGroupContext());
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static ShopService CreateService(QuestBoardContext context)
    {
        var mapper = CreateMapper();
        return new ShopService(new ShopRepository(context, mapper), new UserTransactionRepository(context, mapper), mapper);
    }

    // One board, a buyer, the keeper who created the item, a published item at 40 gp and one purchase of 2 at 80 gp
    // made the given number of hours ago. The purchase moment is a real UTC instant, so the test reads the UTC clock.
    private static async Task SeedAsync(string databaseName, int stock, double purchasedHoursAgo)
    {
        await using var context = CreateContext(databaseName);
        context.Groups.Add(new GroupEntity { Id = BoardId, Name = "Board One" });
        context.UserEntities.Add(new UserEntity { Id = BuyerId, Name = "Buyer", Email = "buyer@test.com" });
        context.UserEntities.Add(new UserEntity { Id = KeeperId, Name = KeeperName, Email = KeeperEmail });
        context.ShopItems.Add(new ShopItemEntity
        {
            Id = ItemId,
            Name = "Rope of Climbing",
            Description = "Sixty feet of silk rope",
            Type = (int)ItemType.Equipment,
            Rarity = (int)ItemRarity.Uncommon,
            Price = 40m,
            Quantity = stock,
            Status = (int)ItemStatus.Published,
            CreatedByDmId = KeeperId,
            GroupId = BoardId
        });
        context.UserTransactions.Add(new UserTransactionEntity
        {
            Id = PurchaseId,
            UserId = BuyerId,
            ShopItemId = ItemId,
            TransactionType = (int)TransactionType.Purchase,
            Price = 80m,
            Quantity = 2,
            TransactionDate = DateTime.UtcNow.AddHours(-purchasedHoursAgo),
            Notes = "Purchase of 2x Rope of Climbing"
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static readonly User Buyer = new() { Id = BuyerId, Name = "Buyer" };

    private static async Task<(ShopItemEntity Item, List<UserTransactionEntity> Rows, UserEntity Keeper)> ReadBackAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var item = await context.ShopItems.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(i => i.Id == ItemId, TestContext.Current.CancellationToken);
        var rows = await context.UserTransactions.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.ShopItemId == ItemId)
            .OrderBy(t => t.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        var keeper = await context.UserEntities.AsNoTracking()
            .SingleAsync(u => u.Id == KeeperId, TestContext.Current.CancellationToken);
        return (item, rows, keeper);
    }

    [Theory]
    [InlineData(2.0, 80.0)]
    [InlineData(30.0, 40.0)]
    public async Task ReturnOrSell_OnLimitedStock_RecordsTheRefundAndPutsTheQuantityBackOnTheShelf(double hoursAgo, double expectedRefund)
    {
        var databaseName = $"{nameof(ReturnOrSell_OnLimitedStock_RecordsTheRefundAndPutsTheQuantityBackOnTheShelf)}_{hoursAgo}";
        await SeedAsync(databaseName, stock: 5, purchasedHoursAgo: hoursAgo);

        await using (var context = CreateContext(databaseName))
        {
            await CreateService(context).ReturnOrSellItemAsync(PurchaseId, 2, Buyer, TestContext.Current.CancellationToken);
        }

        var (item, rows, _) = await ReadBackAsync(databaseName);
        item.Quantity.Should().Be(7, "the two handed-back items go back on a limited shelf");
        var refund = rows.Should().ContainSingle(t => t.OriginalTransactionId == PurchaseId).Subject;
        refund.TransactionType.Should().Be((int)TransactionType.Sell);
        refund.Quantity.Should().Be(2);
        refund.Price.Should().Be((decimal)expectedRefund, "full price inside the return window, half after it");
        var purchase = rows.Should().ContainSingle(t => t.Id == PurchaseId).Subject;
        purchase.ShopItemId.Should().Be(ItemId, "the purchase still belongs to its item");
        purchase.Quantity.Should().Be(2);
        purchase.Price.Should().Be(80m);
    }

    [Fact]
    public async Task ReturnOrSell_OnUnlimitedStock_RecordsTheRefundAndLeavesTheStockUnlimited()
    {
        const string databaseName = nameof(ReturnOrSell_OnUnlimitedStock_RecordsTheRefundAndLeavesTheStockUnlimited);
        await SeedAsync(databaseName, stock: -1, purchasedHoursAgo: 2);

        await using (var context = CreateContext(databaseName))
        {
            await CreateService(context).ReturnOrSellItemAsync(PurchaseId, 2, Buyer, TestContext.Current.CancellationToken);
        }

        var (item, rows, _) = await ReadBackAsync(databaseName);
        item.Quantity.Should().Be(-1);
        rows.Should().ContainSingle(t => t.OriginalTransactionId == PurchaseId).Which.Price.Should().Be(80m);
    }

    [Fact]
    public async Task Purchase_AfterTheBuyersHistoryWasReadInTheSameRequest_TakesTheStock()
    {
        const string databaseName = nameof(Purchase_AfterTheBuyersHistoryWasReadInTheSameRequest_TakesTheStock);
        await SeedAsync(databaseName, stock: 5, purchasedHoursAgo: 2);

        await using (var context = CreateContext(databaseName))
        {
            var service = CreateService(context);
            await service.GetUserTransactionsWithRemainingAsync(BuyerId, TestContext.Current.CancellationToken);
            await service.PurchaseItemAsync(ItemId, 1, Buyer, TestContext.Current.CancellationToken);
        }

        var (item, rows, _) = await ReadBackAsync(databaseName);
        item.Quantity.Should().Be(4);
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(t => t.Id == PurchaseId).Which.ShopItemId.Should().Be(ItemId);
    }

    [Fact]
    public async Task SellToShop_AfterTheSellersHistoryWasReadInTheSameRequest_AddsTheStock()
    {
        const string databaseName = nameof(SellToShop_AfterTheSellersHistoryWasReadInTheSameRequest_AddsTheStock);
        await SeedAsync(databaseName, stock: 5, purchasedHoursAgo: 2);

        await using (var context = CreateContext(databaseName))
        {
            var service = CreateService(context);
            await service.GetUserTransactionsWithRemainingAsync(BuyerId, TestContext.Current.CancellationToken);
            await service.SellItemToShopAsync(ItemId, 3, Buyer, TestContext.Current.CancellationToken);
        }

        var (item, rows, _) = await ReadBackAsync(databaseName);
        item.Quantity.Should().Be(8);
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(t => t.Id == PurchaseId).Which.ShopItemId.Should().Be(ItemId);
    }

    [Fact]
    public async Task UpdatingAnItem_AfterItsPurchaseAndCreatorWereLoaded_WritesOnlyTheItemsOwnColumns()
    {
        const string databaseName = nameof(UpdatingAnItem_AfterItsPurchaseAndCreatorWereLoaded_WritesOnlyTheItemsOwnColumns);
        await SeedAsync(databaseName, stock: 5, purchasedHoursAgo: 2);
        var (before, _, _) = await ReadBackAsync(databaseName);

        await using (var context = CreateContext(databaseName))
        {
            var mapper = CreateMapper();
            var transactions = new UserTransactionRepository(context, mapper);
            var shop = new ShopRepository(context, mapper);

            var purchase = await transactions.GetTransactionWithDetailsAsync(PurchaseId, TestContext.Current.CancellationToken);
            purchase!.ShopItem!.CreatedByDm.Should().NotBeNull("the purchase was loaded with its item and the item's creator");

            var item = await shop.GetByIdAsync(ItemId, TestContext.Current.CancellationToken);
            item!.Name = "Rope of Climbing, renamed";
            item.Quantity = 9;
            if (item.CreatedByDm != null)
            {
                item.CreatedByDm.Name = "Changed on the copy only";
                item.CreatedByDm.Email = "changed@test.com";
            }
            foreach (var row in item.Transactions)
            {
                row.Quantity = 99;
            }

            await shop.UpdateAsync(item, TestContext.Current.CancellationToken);
        }

        var (after, rows, keeper) = await ReadBackAsync(databaseName);
        after.Name.Should().Be("Rope of Climbing, renamed");
        after.Quantity.Should().Be(9);
        after.Should().BeEquivalentTo(before, options => options
            .Excluding(i => i.Name)
            .Excluding(i => i.Quantity)
            .Excluding(i => i.CreatedByDm)
            .Excluding(i => i.Group)
            .Excluding(i => i.Transactions));
        var purchaseRow = rows.Should().ContainSingle().Subject;
        purchaseRow.Id.Should().Be(PurchaseId);
        purchaseRow.ShopItemId.Should().Be(ItemId);
        purchaseRow.Quantity.Should().Be(2, "a saved item does not write its purchases");
        purchaseRow.Price.Should().Be(80m);
        keeper.Name.Should().Be(KeeperName, "a saved item does not write its creator's user row");
        keeper.Email.Should().Be(KeeperEmail);
    }

    private sealed class BoardOneGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => BoardId;
    }
}
