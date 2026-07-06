using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Proves the group-scoped HasQueryFilter predicates on QuestBoardContext are fail-closed:
// a null ActiveGroupId must return zero rows for every group-scoped entity, never every
// group's rows merged together.
public class QuestBoardContextFilterTests
{
    private static QuestBoardContext CreateContext(string databaseName, IActiveGroupContext activeGroupContext)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, activeGroupContext);
    }

    // Seeds two groups' worth of the full Quest-dependent entity graph (Quest, ProposedDate,
    // PlayerSignup + its DateVote, ReminderLog) so every Quest-navigation-scoped filter has
    // real rows to query against. Seeded through a null-ActiveGroupId context so the write path
    // itself isn't blocked by the very filters under test.
    private static async Task SeedQuestGraphAsync(QuestBoardContext context, int groupId, int questId, int playerId, int dmId)
    {
        context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Group {groupId}" });
        context.UserEntities.Add(new UserEntity { Id = dmId, Name = $"DM {dmId}", Email = $"dm{dmId}@test.com" });
        context.UserEntities.Add(new UserEntity { Id = playerId, Name = $"Player {playerId}", Email = $"player{playerId}@test.com" });

        var quest = new QuestEntity
        {
            Id = questId,
            Title = $"Quest {questId}",
            Description = "A quest",
            DungeonMasterId = dmId,
            GroupId = groupId
        };
        context.Quests.Add(quest);

        var proposedDate = new ProposedDateEntity
        {
            Id = questId,
            QuestId = questId,
            Date = DateTime.UtcNow.AddDays(1)
        };
        context.Add(proposedDate);

        var signup = new PlayerSignupEntity
        {
            Id = questId,
            QuestId = questId,
            PlayerId = playerId,
            IsSelected = false,
            SignupRole = 0,
            SignupTime = DateTime.UtcNow
        };
        context.PlayerSignups.Add(signup);

        context.Add(new PlayerDateVoteEntity
        {
            Id = questId,
            PlayerSignupId = signup.Id,
            ProposedDateId = proposedDate.Id,
            Vote = 2
        });

        context.ReminderLogs.Add(new ReminderLogEntity
        {
            Id = questId,
            QuestId = questId,
            PlayerId = playerId,
            SentAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Seeds two groups' worth of ShopItem + UserTransaction rows.
    private static async Task SeedShopGraphAsync(QuestBoardContext context, int groupId, int shopItemId, int buyerId, int dmId)
    {
        if (!await context.Groups.AnyAsync(g => g.Id == groupId, TestContext.Current.CancellationToken))
        {
            context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Group {groupId}" });
        }

        if (!await context.UserEntities.AnyAsync(u => u.Id == dmId, TestContext.Current.CancellationToken))
        {
            context.UserEntities.Add(new UserEntity { Id = dmId, Name = $"DM {dmId}", Email = $"dm{dmId}@test.com" });
        }

        if (!await context.UserEntities.AnyAsync(u => u.Id == buyerId, TestContext.Current.CancellationToken))
        {
            context.UserEntities.Add(new UserEntity { Id = buyerId, Name = $"Buyer {buyerId}", Email = $"buyer{buyerId}@test.com" });
        }

        var shopItem = new ShopItemEntity
        {
            Id = shopItemId,
            Name = $"Item {shopItemId}",
            Description = "A shop item",
            Type = 0,
            Rarity = 0,
            Price = 10m,
            Quantity = 1,
            CreatedByDmId = dmId,
            GroupId = groupId
        };
        context.ShopItems.Add(shopItem);

        context.UserTransactions.Add(new UserTransactionEntity
        {
            Id = shopItemId,
            UserId = buyerId,
            ShopItemId = shopItem.Id,
            TransactionType = 0,
            Price = 10m,
            Quantity = 1,
            TransactionDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------
    // Fail-closed assertions: null ActiveGroupId must yield zero rows
    // -------------------------------------------------------------------

    [Fact]
    public async Task QuestEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(QuestEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedQuestGraphAsync(context, groupId: 1, questId: 1, playerId: 101, dmId: 901);
        await SeedQuestGraphAsync(context, groupId: 2, questId: 2, playerId: 102, dmId: 902);

        var count = await context.Quests.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task ShopItemEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(ShopItemEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedShopGraphAsync(context, groupId: 1, shopItemId: 1, buyerId: 101, dmId: 901);
        await SeedShopGraphAsync(context, groupId: 2, shopItemId: 2, buyerId: 102, dmId: 902);

        var count = await context.ShopItems.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task ProposedDateEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(ProposedDateEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedQuestGraphAsync(context, groupId: 1, questId: 1, playerId: 101, dmId: 901);
        await SeedQuestGraphAsync(context, groupId: 2, questId: 2, playerId: 102, dmId: 902);

        var count = await context.Set<ProposedDateEntity>().CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task PlayerDateVoteEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(PlayerDateVoteEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedQuestGraphAsync(context, groupId: 1, questId: 1, playerId: 101, dmId: 901);
        await SeedQuestGraphAsync(context, groupId: 2, questId: 2, playerId: 102, dmId: 902);

        var count = await context.Set<PlayerDateVoteEntity>().CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task PlayerSignupEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(PlayerSignupEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedQuestGraphAsync(context, groupId: 1, questId: 1, playerId: 101, dmId: 901);
        await SeedQuestGraphAsync(context, groupId: 2, questId: 2, playerId: 102, dmId: 902);

        var count = await context.PlayerSignups.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task ReminderLogEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(ReminderLogEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedQuestGraphAsync(context, groupId: 1, questId: 1, playerId: 101, dmId: 901);
        await SeedQuestGraphAsync(context, groupId: 2, questId: 2, playerId: 102, dmId: 902);

        var count = await context.ReminderLogs.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task UserTransactionEntity_NullActiveGroupId_ReturnsZeroRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(UserTransactionEntity_NullActiveGroupId_ReturnsZeroRows), seedContext);
        await SeedShopGraphAsync(context, groupId: 1, shopItemId: 1, buyerId: 101, dmId: 901);
        await SeedShopGraphAsync(context, groupId: 2, shopItemId: 2, buyerId: 102, dmId: 902);

        var count = await context.UserTransactions.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    // -------------------------------------------------------------------
    // Positive companion test: a concrete ActiveGroupId returns only that group's rows
    // -------------------------------------------------------------------

    [Fact]
    public async Task QuestEntity_ConcreteActiveGroupId_ReturnsOnlyThatGroupsRows()
    {
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var seedDbContext = CreateContext(nameof(QuestEntity_ConcreteActiveGroupId_ReturnsOnlyThatGroupsRows), seedContext);
        await SeedQuestGraphAsync(seedDbContext, groupId: 1, questId: 1, playerId: 101, dmId: 901);
        await SeedQuestGraphAsync(seedDbContext, groupId: 2, questId: 2, playerId: 102, dmId: 902);

        var activeGroupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(QuestEntity_ConcreteActiveGroupId_ReturnsOnlyThatGroupsRows), activeGroupContext);

        var quests = await context.Quests.ToListAsync(TestContext.Current.CancellationToken);

        quests.Should().ContainSingle();
        quests[0].GroupId.Should().Be(1);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
