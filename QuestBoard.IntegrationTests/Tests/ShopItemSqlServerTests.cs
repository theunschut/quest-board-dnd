using Microsoft.Extensions.Configuration;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.IntegrationTests.Migrator;
using QuestBoard.Repository.Automapper;
using QuestBoard.Repository.Extensions;

namespace QuestBoard.IntegrationTests.Tests;

// The in-memory store enforces no foreign key, so it happily stores a shop item with GroupId 0;
// SQL Server refuses it because ShopItems.GroupId references Groups. These facts run the shop
// item writes against a throwaway database migrated to the production schema, so the real
// constraint decides. They skip unless a SQL Server connection is supplied, and they only ever
// create and drop their own uniquely named scratch database.
public class ShopItemSqlServerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedBoardClock : IBoardClock
    {
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        public bool IsDegraded => false;
        public DateOnly Today => new(2031, 1, 1);
        public DateTime Now => new(2031, 1, 1, 12, 0, 0);
    }

    private static ServiceProvider BuildProvider(string connectionString, MutableGroupContext groupContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IActiveGroupContext>(groupContext);
        services.AddSingleton<IBoardClock>(new FixedBoardClock());
        services.AddAutoMapper(cfg => cfg.AddProfile<EntityProfile>());
        services.AddRepositoryServices(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<(int BoardId, int DmId)> MigrateAndSeedAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        await context.Database.MigrateAsync(Token);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var dm = new UserEntity
        {
            Name = "dm",
            UserName = $"dm-{suffix}",
            NormalizedUserName = $"DM-{suffix}".ToUpperInvariant(),
            Email = $"dm-{suffix}@example.test",
            NormalizedEmail = $"dm-{suffix}@example.test".ToUpperInvariant()
        };
        var board = new GroupEntity { Name = $"Shop board {suffix}" };
        context.UserEntities.Add(dm);
        context.Groups.Add(board);
        await context.SaveChangesAsync(Token);
        return (board.Id, dm.Id);
    }

    private static ShopItem NewItem(int dmId, int groupId) => new()
    {
        Name = "Scratch Lantern",
        Description = "Throwaway item",
        Type = ItemType.Equipment,
        Rarity = ItemRarity.Rare,
        Price = 12m,
        Quantity = 3,
        Status = ItemStatus.Draft,
        CreatedByDmId = dmId,
        GroupId = groupId
    };

    [Fact]
    public async Task AnItemWithoutABoard_IsRefusedByTheGroupForeignKey()
    {
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var scratch = new ScratchDatabase(server);
        var groupContext = new MutableGroupContext { ActiveGroupId = null };
        await using var provider = BuildProvider(scratch.ConnectionString, groupContext);
        var (_, dmId) = await MigrateAndSeedAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShopRepository>();

        var act = () => repository.AddAsync(NewItem(dmId, groupId: 0), Token);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*FK_ShopItems_Groups_GroupId*");
    }

    [Fact]
    public async Task AnItemOnTheActiveBoard_CanBeCreatedEditedAndDeleted()
    {
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var scratch = new ScratchDatabase(server);
        var groupContext = new MutableGroupContext { ActiveGroupId = null };
        await using var provider = BuildProvider(scratch.ConnectionString, groupContext);
        var (boardId, dmId) = await MigrateAndSeedAsync(provider);
        groupContext.ActiveGroupId = boardId;

        // Create
        int itemId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IShopRepository>();
            var item = NewItem(dmId, boardId);
            await repository.AddAsync(item, Token);
            itemId = item.Id;
        }

        itemId.Should().BePositive();
        MigratorSqlServer.Scalar<int>(scratch.ConnectionString,
            "SELECT [GroupId] FROM [dbo].[ShopItems] WHERE [Id] = @id", ("@id", itemId)).Should().Be(boardId);

        // Edit: the controller loads the item and its DM in the same request, so the DM row is
        // tracked by the same context when the update is written.
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            await context.UserEntities.SingleAsync(u => u.Id == dmId, Token);
            var repository = scope.ServiceProvider.GetRequiredService<IShopRepository>();
            var item = await repository.GetByIdAsync(itemId, Token);
            item.Should().NotBeNull("the item is on the active board");
            item!.Name = "Edited Scratch Lantern";
            item.Rarity = ItemRarity.Common;
            item.Status = ItemStatus.Published;
            await repository.UpdateAsync(item, Token);
        }

        MigratorSqlServer.Scalar<string>(scratch.ConnectionString,
            "SELECT [Name] FROM [dbo].[ShopItems] WHERE [Id] = @id", ("@id", itemId)).Should().Be("Edited Scratch Lantern");
        MigratorSqlServer.Scalar<int>(scratch.ConnectionString,
            "SELECT [GroupId] FROM [dbo].[ShopItems] WHERE [Id] = @id", ("@id", itemId)).Should().Be(boardId);

        // Delete
        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IShopRepository>();
            var item = await repository.GetByIdAsync(itemId, Token);
            await repository.RemoveAsync(item!, Token);
        }

        MigratorSqlServer.Scalar<int>(scratch.ConnectionString,
            "SELECT COUNT(*) FROM [dbo].[ShopItems] WHERE [Id] = @id", ("@id", itemId)).Should().Be(0);
    }
}
