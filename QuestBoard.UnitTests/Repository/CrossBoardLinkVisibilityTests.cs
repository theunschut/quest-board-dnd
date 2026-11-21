using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
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

    // ---- Quests ----

    private static QuestEntity QuestRow(int id, int dungeonMasterId, int groupId = BoardOne, bool finalized = false, DateTime? finalizedDate = null, bool closed = false, bool dungeonMasterSession = false)
        => new()
        {
            Id = id,
            Title = $"Quest {id}",
            Description = "A quest for the visibility rule",
            DungeonMasterId = dungeonMasterId,
            GroupId = groupId,
            IsFinalized = finalized,
            FinalizedDate = finalizedDate,
            IsClosed = closed,
            DungeonMasterSession = dungeonMasterSession
        };

    [Fact]
    public async Task QuestOwnerOrAdmin_ResolvesForTheQuestsOwnDungeonMaster()
    {
        await using var context = CreateContext(DatabaseName(nameof(QuestOwnerOrAdmin_ResolvesForTheQuestsOwnDungeonMaster)));
        await SeedBoardsAndUsersAsync(context);
        context.Quests.Add(QuestRow(1, dungeonMasterId: ViewerId));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, 1, scope)).Should().Be(BoardOne);
    }

    [Fact]
    public async Task QuestOwnerOrAdmin_DoesNotResolveForAnotherDungeonMaster()
    {
        await using var context = CreateContext(DatabaseName(nameof(QuestOwnerOrAdmin_DoesNotResolveForAnotherDungeonMaster)));
        await SeedBoardsAndUsersAsync(context);
        context.Quests.Add(QuestRow(1, dungeonMasterId: OtherUserId));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var scope = Scope([BoardOne], dungeonMasterTier: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, 1, scope)).Should().BeNull();
    }

    [Fact]
    public async Task QuestOwnerOrAdmin_ResolvesForAnAdminOrSuperAdminMemberOfTheBoard()
    {
        await using var context = CreateContext(DatabaseName(nameof(QuestOwnerOrAdmin_ResolvesForAnAdminOrSuperAdminMemberOfTheBoard)));
        await SeedBoardsAndUsersAsync(context);
        context.Quests.Add(QuestRow(1, dungeonMasterId: OtherUserId));
        context.Quests.Add(QuestRow(2, dungeonMasterId: OtherUserId, groupId: BoardTwo));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Admin on board one only: the quest on board two belongs to another Dungeon Master there.
        var scope = Scope([BoardOne, BoardTwo], dungeonMasterTier: [BoardOne, BoardTwo], admin: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, 1, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, 2, scope)).Should().BeNull("the viewer is only a Dungeon Master there, and the quest is another's");
    }

    [Fact]
    public async Task QuestWithNoRecordRule_ResolvesForAnyCandidateBoard()
    {
        await using var context = CreateContext(DatabaseName(nameof(QuestWithNoRecordRule_ResolvesForAnyCandidateBoard)));
        await SeedBoardsAndUsersAsync(context);
        context.Quests.Add(QuestRow(1, dungeonMasterId: OtherUserId));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var scope = Scope([BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.Member, 1, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.DungeonMasterTier, 1, scope)).Should().Be(BoardOne);
    }

    [Fact]
    public async Task CompletedQuest_ResolvesForAFinalizedQuestBeforeTodayAndForAClosedQuest_NotForTheRest()
    {
        await using var context = CreateContext(DatabaseName(nameof(CompletedQuest_ResolvesForAFinalizedQuestBeforeTodayAndForAClosedQuest_NotForTheRest)));
        await SeedBoardsAndUsersAsync(context);
        context.Quests.Add(QuestRow(1, OtherUserId, finalized: true, finalizedDate: BoardTodayStart.AddDays(-1)));
        context.Quests.Add(QuestRow(2, OtherUserId, closed: true));
        context.Quests.Add(QuestRow(3, OtherUserId));
        context.Quests.Add(QuestRow(4, OtherUserId, finalized: true, finalizedDate: BoardTodayStart.AddHours(20)));
        context.Quests.Add(QuestRow(5, OtherUserId, finalized: true, finalizedDate: BoardTodayStart.AddDays(30)));
        context.Quests.Add(QuestRow(6, OtherUserId, finalized: true, finalizedDate: BoardTodayStart.AddDays(-1), dungeonMasterSession: true));
        context.Quests.Add(QuestRow(7, OtherUserId, closed: true, dungeonMasterSession: true));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var scope = Scope([BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 1, scope)).Should().Be(BoardOne, "played yesterday");
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 2, scope)).Should().Be(BoardOne, "a closed quest is in the log");
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 3, scope)).Should().BeNull("an open quest is not in the log");
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 4, scope)).Should().BeNull("a game night later today has not been played");
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 5, scope)).Should().BeNull("a game night in the future has not been played");
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 6, scope)).Should().BeNull("a Dungeon Master session is not shown in the log");
        (await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 7, scope)).Should().Be(BoardOne, "a closed quest is shown even when it was a Dungeon Master session");
    }

    // Finalized dates either side of every boundary of the board's today, with the finalized flag on and off.
    public static TheoryData<int> FinalizedDateOffsetsInMinutes => new()
    {
        -3 * 24 * 60,
        -24 * 60,
        -24 * 60 + 1,
        -1,
        0,
        1,
        12 * 60,
        24 * 60 - 1,
        24 * 60,
        7 * 24 * 60,
    };

    [Theory]
    [MemberData(nameof(FinalizedDateOffsetsInMinutes))]
    public async Task CompletedQuest_AgreesWithHasBeenPlayedForOneShots(int offsetMinutes)
    {
        await using var context = CreateContext(DatabaseName($"{nameof(CompletedQuest_AgreesWithHasBeenPlayedForOneShots)}_{offsetMinutes}"));
        await SeedBoardsAndUsersAsync(context);
        var finalizedDate = BoardTodayStart.AddMinutes(offsetMinutes);
        context.Quests.Add(QuestRow(1, OtherUserId, finalized: true, finalizedDate: finalizedDate));
        context.Quests.Add(QuestRow(2, OtherUserId, finalized: false, finalizedDate: finalizedDate));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var boardToday = DateOnly.FromDateTime(BoardTodayStart);
        var finalizedModel = new Quest { Id = 1, IsFinalized = true, FinalizedDate = finalizedDate };
        var unfinalizedModel = new Quest { Id = 2, IsFinalized = false, FinalizedDate = finalizedDate };
        var scope = Scope([BoardOne]);

        var finalizedResolves = await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 1, scope) is not null;
        var unfinalizedResolves = await ResolveAsync(context, CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, 2, scope) is not null;

        finalizedResolves.Should().Be(finalizedModel.HasBeenPlayed(boardToday), "the log and the switch apply one definition of played");
        unfinalizedResolves.Should().Be(unfinalizedModel.HasBeenPlayed(boardToday));
    }

    // ---- Characters ----

    private static async Task SeedCharactersAsync(QuestBoardContext context)
    {
        await SeedBoardsAndUsersAsync(context);
        context.Characters.Add(new CharacterEntity { Id = 1, Name = "Mine", OwnerId = ViewerId, GroupId = BoardOne });
        context.Characters.Add(new CharacterEntity { Id = 2, Name = "Theirs", OwnerId = OtherUserId, GroupId = BoardOne });
        context.Characters.Add(new CharacterEntity { Id = 3, Name = "Theirs elsewhere", OwnerId = OtherUserId, GroupId = BoardTwo });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CharacterOwnerOrAdmin_ResolvesForTheOwner_NotForAnotherPlayer()
    {
        await using var context = CreateContext(DatabaseName(nameof(CharacterOwnerOrAdmin_ResolvesForTheOwner_NotForAnotherPlayer)));
        await SeedCharactersAsync(context);
        var scope = Scope([BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Character, CrossBoardAccess.CharacterOwnerOrAdmin, 1, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Character, CrossBoardAccess.CharacterOwnerOrAdmin, 2, scope)).Should().BeNull();
    }

    [Fact]
    public async Task CharacterOwnerOrAdmin_ResolvesForAnAdminOrSuperAdminMemberOfTheBoardOnly()
    {
        await using var context = CreateContext(DatabaseName(nameof(CharacterOwnerOrAdmin_ResolvesForAnAdminOrSuperAdminMemberOfTheBoardOnly)));
        await SeedCharactersAsync(context);
        var scope = Scope([BoardOne, BoardTwo], dungeonMasterTier: [BoardOne], admin: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Character, CrossBoardAccess.CharacterOwnerOrAdmin, 2, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Character, CrossBoardAccess.CharacterOwnerOrAdmin, 3, scope)).Should().BeNull("the viewer is not an Admin of that board");
    }

    [Fact]
    public async Task CharacterDetails_AnyMemberResolves()
    {
        await using var context = CreateContext(DatabaseName(nameof(CharacterDetails_AnyMemberResolves)));
        await SeedCharactersAsync(context);

        (await ResolveAsync(context, CrossBoardLookupKind.Character, CrossBoardAccess.Member, 2, Scope([BoardOne]))).Should().Be(BoardOne);
    }

    // ---- Contacts ----

    private static async Task SeedContactsAsync(QuestBoardContext context)
    {
        await SeedBoardsAndUsersAsync(context);
        context.Contacts.Add(new ContactEntity { Id = 1, Name = "Revealed", IsRevealed = true, CreatedByUserId = OtherUserId, GroupId = BoardOne });
        context.Contacts.Add(new ContactEntity { Id = 2, Name = "Hidden", IsRevealed = false, CreatedByUserId = OtherUserId, GroupId = BoardOne });
        context.Contacts.Add(new ContactEntity { Id = 3, Name = "Hidden mine", IsRevealed = false, CreatedByUserId = ViewerId, GroupId = BoardOne });
        context.Contacts.Add(new ContactEntity { Id = 4, Name = "Hidden elsewhere", IsRevealed = false, CreatedByUserId = OtherUserId, GroupId = BoardTwo });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ContactVisible_ForAPlayer_ResolvesRevealedAndOwnContactsOnly()
    {
        await using var context = CreateContext(DatabaseName(nameof(ContactVisible_ForAPlayer_ResolvesRevealedAndOwnContactsOnly)));
        await SeedContactsAsync(context);
        var scope = Scope([BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, 1, scope)).Should().Be(BoardOne, "revealed");
        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, 3, scope)).Should().Be(BoardOne, "the viewer created it");
        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, 2, scope)).Should().BeNull("hidden and created by someone else");
    }

    [Fact]
    public async Task ContactVisible_ForADungeonMasterTierViewer_ResolvesHiddenContactsOnThatBoardOnly()
    {
        await using var context = CreateContext(DatabaseName(nameof(ContactVisible_ForADungeonMasterTierViewer_ResolvesHiddenContactsOnThatBoardOnly)));
        await SeedContactsAsync(context);
        var scope = Scope([BoardOne, BoardTwo], dungeonMasterTier: [BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, 2, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, 4, scope)).Should().BeNull("the viewer is only a Player on that board");
    }

    [Fact]
    public async Task ContactEdit_KeepsOnlyTheMembershipPin()
    {
        await using var context = CreateContext(DatabaseName(nameof(ContactEdit_KeepsOnlyTheMembershipPin)));
        await SeedContactsAsync(context);

        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.DungeonMasterTier, 2, Scope([BoardOne], dungeonMasterTier: [BoardOne]))).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Contact, CrossBoardAccess.DungeonMasterTier, 4, Scope([BoardOne], dungeonMasterTier: [BoardOne]))).Should().BeNull("board two is not among the candidates");
    }

    // ---- Kinds checked and left as they are ----

    [Fact]
    public async Task EventAndEventSeries_ResolveForAnyCandidateBoardWithNoRecordRule()
    {
        await using var context = CreateContext(DatabaseName(nameof(EventAndEventSeries_ResolveForAnyCandidateBoardWithNoRecordRule)));
        await SeedBoardsAndUsersAsync(context);
        context.Events.Add(new EventEntity { Id = 1, Title = "Cancelled", Description = "An event", Date = DateOnly.FromDateTime(BoardTodayStart.AddDays(3)), StartTime = new TimeOnly(19, 0), CancelledAt = BoardTodayStart, GroupId = BoardOne });
        context.EventSeries.Add(new EventSeriesEntity { Id = 1, Title = "Series", GroupId = BoardOne });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var scope = Scope([BoardOne]);

        (await ResolveAsync(context, CrossBoardLookupKind.Event, CrossBoardAccess.Member, 1, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.EventSeries, CrossBoardAccess.Member, 1, scope)).Should().Be(BoardOne);
        (await ResolveAsync(context, CrossBoardLookupKind.Event, CrossBoardAccess.Member, 1, Scope([BoardTwo]))).Should().BeNull("the membership pin still applies");
    }

    private sealed class BoardOneGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => 1;
    }
}
