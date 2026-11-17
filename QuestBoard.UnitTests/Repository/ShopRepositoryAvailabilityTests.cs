using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// The shop list queries filter on the board-local time they are handed. Whatever that time is, they must return
// exactly the items the shared on-sale rule accepts, so the list and a purchase by id can never disagree.
public class ShopRepositoryAvailabilityTests
{
    private const int KeeperId = 900;

    private static readonly DateTime BoardNow = new(2026, 9, 6, 0, 30, 0);

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

    // Items in every status, around the boundary minutes and days of the board-local moment above.
    private static readonly (ItemStatus Status, DateTime? From, DateTime? Until)[] Shapes =
    [
        (ItemStatus.Published, null, null),
        (ItemStatus.Published, new DateTime(2026, 9, 6, 0, 30, 0), null),
        (ItemStatus.Published, new DateTime(2026, 9, 6, 0, 0, 0), null),
        (ItemStatus.Published, new DateTime(2026, 9, 6, 0, 31, 0), null),
        (ItemStatus.Published, new DateTime(2026, 9, 7, 0, 0, 0), null),
        (ItemStatus.Published, null, new DateTime(2026, 9, 6, 0, 30, 0)),
        (ItemStatus.Published, null, new DateTime(2026, 9, 6, 0, 29, 0)),
        (ItemStatus.Published, null, new DateTime(2026, 9, 5, 23, 59, 0)),
        (ItemStatus.Published, new DateTime(2026, 9, 1, 0, 0, 0), new DateTime(2026, 9, 30, 0, 0, 0)),
        (ItemStatus.Draft, null, null),
        (ItemStatus.Denied, new DateTime(2026, 9, 1, 0, 0, 0), new DateTime(2026, 9, 30, 0, 0, 0)),
        (ItemStatus.Archived, null, null),
    ];

    private static async Task<List<ShopItem>> SeedAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        context.Groups.Add(new GroupEntity { Id = 1, Name = "Board One" });
        context.UserEntities.Add(new UserEntity { Id = KeeperId, Name = "Keeper", Email = "keeper@test.com" });
        for (var i = 0; i < Shapes.Length; i++)
        {
            context.ShopItems.Add(new ShopItemEntity
            {
                Id = i + 1,
                Name = $"Item {i + 1:00}",
                Description = "An item for the availability window",
                Type = (int)ItemType.Equipment,
                Rarity = (int)ItemRarity.Common,
                Price = 10m,
                Quantity = 5,
                Status = (int)Shapes[i].Status,
                AvailableFrom = Shapes[i].From,
                AvailableUntil = Shapes[i].Until,
                CreatedByDmId = KeeperId,
                GroupId = 1
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entities = await context.ShopItems.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        return CreateMapper().Map<List<ShopItem>>(entities);
    }

    public static TheoryData<DateTime> BoardTimes => new()
    {
        BoardNow,
        new DateTime(2026, 9, 7, 0, 0, 0),
        new DateTime(2026, 9, 5, 23, 59, 0),
    };

    [Theory]
    [MemberData(nameof(BoardTimes))]
    public async Task TheThreeListQueries_ReturnExactlyTheItemsTheRuleCallsOnSale(DateTime boardNow)
    {
        var databaseName = $"{nameof(TheThreeListQueries_ReturnExactlyTheItemsTheRuleCallsOnSale)}_{boardNow:yyyyMMddHHmm}";
        var items = await SeedAsync(databaseName);
        var expected = items.Where(i => ShopItemAvailability.IsOnSale(i, boardNow)).Select(i => i.Id).ToList();
        expected.Should().NotBeEmpty();

        await using var context = CreateContext(databaseName);
        var repository = new ShopRepository(context, CreateMapper());

        var paged = await repository.GetPagedPublishedItemsAsync(null, null, null, null, 1, 100, boardNow, TestContext.Current.CancellationToken);
        var published = await repository.GetPublishedItemsAsync(boardNow, TestContext.Current.CancellationToken);
        var byType = await repository.GetItemsByTypeAsync((int)ItemType.Equipment, boardNow, TestContext.Current.CancellationToken);

        paged.Items.Select(i => i.Id).Should().BeEquivalentTo(expected);
        paged.TotalCount.Should().Be(expected.Count);
        published.Select(i => i.Id).Should().BeEquivalentTo(expected);
        byType.Select(i => i.Id).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ALaterBoardTime_ChangesWhichItemsAreListed()
    {
        const string databaseName = nameof(ALaterBoardTime_ChangesWhichItemsAreListed);
        await SeedAsync(databaseName);

        await using var context = CreateContext(databaseName);
        var repository = new ShopRepository(context, CreateMapper());

        var now = await repository.GetPublishedItemsAsync(BoardNow, TestContext.Current.CancellationToken);
        var later = await repository.GetPublishedItemsAsync(new DateTime(2026, 9, 7, 0, 0, 0), TestContext.Current.CancellationToken);

        later.Select(i => i.Id).Should().NotBeEquivalentTo(now.Select(i => i.Id));
        later.Select(i => i.Id).Should().Contain([4, 5], "both scheduled items have opened by the next board day");
        later.Select(i => i.Id).Should().NotContain(6, "an end at half past midnight on the 6th has passed by the 7th");
    }

    private sealed class BoardOneGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => 1;
    }
}
