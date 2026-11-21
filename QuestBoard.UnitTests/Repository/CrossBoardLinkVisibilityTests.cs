using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// The cross-board lookup applies, inside its one membership-pinned query, the same rule the target
// page applies to the record. These facts run each record rule against an in-memory store with the
// repository constructed directly, and pair every rule that mirrors a shared Domain rule with a
// parity fact so the page and the switch cannot drift apart.
public class CrossBoardLinkVisibilityTests
{
    private const int ViewerId = 7;
    private const int OtherUserId = 8;
    private const int BoardOne = 1;
    private const int BoardTwo = 2;

    private static readonly DateTime BoardNow = new(2026, 9, 6, 0, 30, 0);
    private static readonly DateTime BoardTodayStart = new(2026, 9, 6, 0, 0, 0);

    private static QuestBoardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new BoardOneGroupContext());
    }

    private static string DatabaseName(string fact) => $"{nameof(CrossBoardLinkVisibilityTests)}_{fact}_{Guid.NewGuid():N}";

    private static async Task SeedBoardsAndUsersAsync(QuestBoardContext context)
    {
        context.Groups.Add(new GroupEntity { Id = BoardOne, Name = "Board One" });
        context.Groups.Add(new GroupEntity { Id = BoardTwo, Name = "Board Two" });
        context.UserEntities.Add(new UserEntity { Id = ViewerId, Name = "Viewer", Email = "viewer@test.com" });
        context.UserEntities.Add(new UserEntity { Id = OtherUserId, Name = "Other", Email = "other@test.com" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The viewer's candidate boards are the ones passed in; the two sets that raise the viewer's reach are empty
    // unless the fact says otherwise.
    private static CrossBoardVisibilityScope Scope(
        int[] candidates,
        int[]? dungeonMasterTier = null,
        int[]? admin = null,
        int viewerId = ViewerId,
        DateTime? boardNow = null,
        DateTime? boardTodayStart = null)
        => new(viewerId, candidates, dungeonMasterTier ?? [], admin ?? [], boardNow ?? BoardNow, boardTodayStart ?? BoardTodayStart);

    private static Task<int?> ResolveAsync(QuestBoardContext context, CrossBoardLookupKind kind, CrossBoardAccess access, int id, CrossBoardVisibilityScope scope)
        => new CrossBoardLinkRepository(context).ResolveBoardIdAsync(kind, access, id, scope, TestContext.Current.CancellationToken);

    // ---- Shop items ----

    // Every status around the boundary minutes of the board-local moment above.
    private static readonly (ItemStatus Status, DateTime? From, DateTime? Until)[] ShopShapes =
    [
        (ItemStatus.Published, null, null),
        (ItemStatus.Published, new DateTime(2026, 9, 6, 0, 30, 0), null),
        (ItemStatus.Published, new DateTime(2026, 9, 6, 0, 31, 0), null),
        (ItemStatus.Published, new DateTime(2026, 9, 7, 0, 0, 0), null),
        (ItemStatus.Published, new DateTime(2026, 9, 1, 0, 0, 0), new DateTime(2026, 9, 5, 0, 0, 0)),
        (ItemStatus.Archived, null, null),
        (ItemStatus.Archived, new DateTime(2026, 9, 7, 0, 0, 0), null),
        (ItemStatus.Draft, null, null),
        (ItemStatus.Draft, new DateTime(2026, 9, 1, 0, 0, 0), null),
        (ItemStatus.Denied, null, null),
        (ItemStatus.Denied, new DateTime(2026, 9, 7, 0, 0, 0), null),
    ];

    private static async Task SeedShopItemsAsync(QuestBoardContext context, int groupId = BoardOne)
    {
        await SeedBoardsAndUsersAsync(context);
        for (var i = 0; i < ShopShapes.Length; i++)
        {
            context.ShopItems.Add(new ShopItemEntity
            {
                Id = i + 1,
                Name = $"Item {i + 1:00}",
                Description = "An item for the visibility rule",
                Type = (int)ItemType.Equipment,
                Rarity = (int)ItemRarity.Common,
                Price = 10m,
                Quantity = 5,
                Status = (int)ShopShapes[i].Status,
                AvailableFrom = ShopShapes[i].From,
                AvailableUntil = ShopShapes[i].Until,
                CreatedByDmId = OtherUserId,
                GroupId = groupId
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ShopItem ModelOf(int index) => new()
    {
        Id = index + 1,
        Name = $"Item {index + 1:00}",
        Status = ShopShapes[index].Status,
        AvailableFrom = ShopShapes[index].From,
        AvailableUntil = ShopShapes[index].Until
    };

    public static TheoryData<DateTime> ShopBoardTimes => new()
    {
        BoardNow,
        new DateTime(2026, 9, 7, 0, 0, 0),
        new DateTime(2026, 9, 5, 23, 59, 0),
    };

    [Theory]
    [MemberData(nameof(ShopBoardTimes))]
    public async Task ShopItem_ForAPlayer_ResolvesExactlyWhenTheSharedRuleSaysMembersMaySeeIt(DateTime boardNow)
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_ForAPlayer_ResolvesExactlyWhenTheSharedRuleSaysMembersMaySeeIt)));
        await SeedShopItemsAsync(context);
        var scope = Scope([BoardOne], boardNow: boardNow);

        var resolvedIds = new List<int>();
        var expectedIds = new List<int>();
        for (var i = 0; i < ShopShapes.Length; i++)
        {
            if (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, i + 1, scope) is not null)
            {
                resolvedIds.Add(i + 1);
            }

            if (ShopItemAvailability.IsVisibleToMembers(ModelOf(i), boardNow))
            {
                expectedIds.Add(i + 1);
            }
        }

        expectedIds.Should().NotBeEmpty();
        expectedIds.Count.Should().BeLessThan(ShopShapes.Length, "the rows include items members may not see");
        resolvedIds.Should().BeEquivalentTo(expectedIds, "the switch and the page apply one rule");
    }

    [Fact]
    public async Task ShopItem_ForAViewerWhoseTierIncludesTheBoard_ResolvesEveryStatusAndWindow()
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_ForAViewerWhoseTierIncludesTheBoard_ResolvesEveryStatusAndWindow)));
        await SeedShopItemsAsync(context);
        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne]);

        for (var i = 0; i < ShopShapes.Length; i++)
        {
            (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, i + 1, scope))
                .Should().Be(BoardOne, $"row {i + 1} ({ShopShapes[i].Status}) is open to a Dungeon Master, Admin or SuperAdmin member");
        }
    }

    // The Draft is row 8 and the scheduled item is row 3.
    [Fact]
    public async Task ShopItem_ForAnAdminWhoIsNotADungeonMaster_ResolvesTheDraftAndTheScheduledItem()
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_ForAnAdminWhoIsNotADungeonMaster_ResolvesTheDraftAndTheScheduledItem)));
        await SeedShopItemsAsync(context);
        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne], admin: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 8, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 3, scope)).Should().Be(BoardOne);
    }

    [Fact]
    public async Task ShopItem_ForASuperAdminWhoIsAPlayerMember_ResolvesTheDraftAndTheScheduledItem()
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_ForASuperAdminWhoIsAPlayerMember_ResolvesTheDraftAndTheScheduledItem)));
        await SeedShopItemsAsync(context);

        // The resolver places a SuperAdmin's member board in the Dungeon Master tier whatever their role there is.
        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne], admin: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 8, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 3, scope)).Should().Be(BoardOne);
    }

    [Fact]
    public async Task ShopItem_ForAPlainPlayer_ResolvesNeitherTheDraftNorTheScheduledItem()
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_ForAPlainPlayer_ResolvesNeitherTheDraftNorTheScheduledItem)));
        await SeedShopItemsAsync(context);
        var scope = Scope([BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 8, scope)).Should().BeNull();
        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 3, scope)).Should().BeNull();
        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 10, scope)).Should().BeNull("a Denied item is hidden too");
    }

    [Fact]
    public async Task ShopItem_OnABoardOutsideTheCandidates_NeverResolves_WhateverTheTier()
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_OnABoardOutsideTheCandidates_NeverResolves_WhateverTheTier)));
        await SeedShopItemsAsync(context, groupId: BoardTwo);

        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, 1, scope)).Should().BeNull();
    }

    [Fact]
    public async Task ShopItem_ForTheManagementPage_KeepsOnlyTheMembershipPin()
    {
        await using var context = CreateContext(DatabaseName(nameof(ShopItem_ForTheManagementPage_KeepsOnlyTheMembershipPin)));
        await SeedShopItemsAsync(context);
        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.DungeonMasterTier, 8, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.ShopItem, CrossBoardAccess.DungeonMasterTier, 999, scope)).Should().BeNull();
    }

    private sealed class BoardOneGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => 1;
    }
}
