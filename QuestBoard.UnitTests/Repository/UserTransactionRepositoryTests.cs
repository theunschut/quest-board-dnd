using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class UserTransactionRepositoryTests
{
    private static QuestBoardContext CreateContext(string databaseName, MutableTestGroupContext groupContext)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, groupContext);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    // Seeds two groups, a buyer user, a DM (item owner) user, one ShopItem per group,
    // and one Purchase transaction per ShopItem, both owned by the same buyer. Returns
    // the group-1 and group-2 ShopItem IDs so tests can assert on them.
    private static async Task<(int Group1ShopItemId, int Group2ShopItemId)> SeedAsync(QuestBoardContext context, int buyerId, int dmId)
    {
        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.Groups.Add(new GroupEntity { Id = 2, Name = "Group Two" });

        context.UserEntities.Add(new UserEntity { Id = buyerId, Name = "Buyer", Email = "buyer@test.com" });
        context.UserEntities.Add(new UserEntity { Id = dmId, Name = "DM", Email = "dm@test.com" });

        var group1Item = new ShopItemEntity
        {
            Id = 1,
            Name = "Group 1 Item",
            Description = "An item in group 1",
            Type = 0,
            Rarity = 0,
            Price = 10m,
            Quantity = 10,
            CreatedByDmId = dmId,
            GroupId = 1
        };

        var group2Item = new ShopItemEntity
        {
            Id = 2,
            Name = "Group 2 Item",
            Description = "An item in group 2",
            Type = 0,
            Rarity = 0,
            Price = 20m,
            Quantity = 10,
            CreatedByDmId = dmId,
            GroupId = 2
        };

        context.ShopItems.Add(group1Item);
        context.ShopItems.Add(group2Item);

        context.UserTransactions.Add(new UserTransactionEntity
        {
            Id = 1,
            UserId = buyerId,
            ShopItemId = group1Item.Id,
            TransactionType = (int)TransactionType.Purchase,
            Price = 10m,
            Quantity = 1,
            TransactionDate = DateTime.UtcNow
        });

        context.UserTransactions.Add(new UserTransactionEntity
        {
            Id = 2,
            UserId = buyerId,
            ShopItemId = group2Item.Id,
            TransactionType = (int)TransactionType.Purchase,
            Price = 20m,
            Quantity = 1,
            TransactionDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (group1Item.Id, group2Item.Id);
    }

    [Fact]
    public async Task GetTransactionsByUserAsync_TransactionForCrossGroupShopItem_IsExcluded()
    {
        // Arrange: seed with no active group so all rows are visible while writing, then
        // switch the active group to 1 before reading.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetTransactionsByUserAsync_TransactionForCrossGroupShopItem_IsExcluded), groupContext);
        const int buyerId = 500;
        const int dmId = 501;
        var (_, group2ShopItemId) = await SeedAsync(context, buyerId, dmId);

        groupContext.ActiveGroupId = 1;
        var repository = new UserTransactionRepository(context, CreateMapper());

        // Act
        var result = await repository.GetTransactionsByUserAsync(buyerId, TestContext.Current.CancellationToken);

        // Assert: this proves the Include-driven inner join protection — GetTransactionsByUserAsync
        // Includes ShopItem, and ShopItem's own query filter folds into that join as an inner join,
        // silently dropping any UserTransaction whose ShopItem belongs to a different group. If a
        // future refactor drops the .Include(t => t.ShopItem), this test fails loudly.
        result.Should().NotContain(t => t.ShopItemId == group2ShopItemId);
    }

    [Fact]
    public async Task GetTransactionsByUserAsync_TransactionForSameGroupShopItem_IsReturned()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetTransactionsByUserAsync_TransactionForSameGroupShopItem_IsReturned), groupContext);
        const int buyerId = 500;
        const int dmId = 501;
        var (group1ShopItemId, _) = await SeedAsync(context, buyerId, dmId);

        groupContext.ActiveGroupId = 1;
        var repository = new UserTransactionRepository(context, CreateMapper());

        // Act
        var result = await repository.GetTransactionsByUserAsync(buyerId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Contain(t => t.ShopItemId == group1ShopItemId);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
