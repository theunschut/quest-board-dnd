using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.Repository.Entities;
using Xunit.Sdk;
using static QuestBoard.UnitTests.Repository.BoardDeletionTestGraph;

namespace QuestBoard.UnitTests.Repository;

// Proves the board delete against the in-memory store. Each fact seeds through one context and
// deletes through a fresh one from the same store, the way a request does, then runs the
// foreign-key sweep because the in-memory store enforces no foreign key of its own.
public class GroupRepositoryBoardDeletionTests
{
    private const int Doomed = 10;
    private const int Other = 20;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SeededBoard> SeedQuestBoardAsync(Store store)
    {
        await using var context = store.NewContext();
        await SeedBoardsAndUsersAsync(context, Doomed, Other);
        return await SeedQuestContentAsync(context, Doomed, "Doomed");
    }

    [Fact]
    public async Task Delete_ReportsRemovedCountsForQuestContent()
    {
        var store = new Store();
        var seeded = await SeedQuestBoardAsync(store);

        BoardDeletionResult result;
        await using (var context = store.NewContext())
        {
            result = await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
        }

        result.Status.Should().Be(BoardDeleteStatus.Deleted);
        foreach (var kind in seeded.Counts.Keys)
        {
            result.RemovedCounts[kind].Should().Be(seeded.Counts[kind], because: $"{kind} was seeded that many times");
        }

        await using var assertContext = store.NewContext();
        var remaining = await CountBoardRowsAsync(assertContext, Doomed);
        remaining.Values.Should().OnlyContain(count => count == 0);
        (await assertContext.Groups.CountAsync(g => g.Id == Doomed, Token)).Should().Be(0);
        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }

    [Fact]
    public async Task Delete_WhenTheBoardHasMembers_RefusesAndChangesNothing()
    {
        var store = new Store();
        await SeedQuestBoardAsync(store);
        await using (var seed = store.NewContext())
        {
            seed.UserGroups.Add(new UserGroupEntity { UserId = PlayerUserId, GroupId = Doomed, GroupRole = (int)GroupRole.Player });
            await seed.SaveChangesAsync(Token);
        }

        IReadOnlyDictionary<BoardContentKind, int> before;
        await using (var context = store.NewContext())
        {
            before = await CountBoardRowsAsync(context, Doomed);
        }

        await using var deleteContext = store.NewContext();
        var result = await CreateRepository(deleteContext).DeleteBoardAsync(Doomed, Token);

        result.Status.Should().Be(BoardDeleteStatus.HasMembers);
        result.RemovedCounts.Should().BeEmpty();

        await using var assertContext = store.NewContext();
        (await CountBoardRowsAsync(assertContext, Doomed)).Should().BeEquivalentTo(before);
        (await assertContext.Groups.CountAsync(g => g.Id == Doomed, Token)).Should().Be(1);
        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }

    [Fact]
    public async Task Delete_WhenTheBoardDoesNotExist_ReturnsNotFound()
    {
        var store = new Store();
        await SeedQuestBoardAsync(store);

        await using var context = store.NewContext();
        var result = await CreateRepository(context).DeleteBoardAsync(9999, Token);

        result.Status.Should().Be(BoardDeleteStatus.NotFound);
        result.RemovedCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_SavesExactlyOnce()
    {
        var store = new Store();
        await SeedQuestBoardAsync(store);

        await using var context = store.NewContext();
        var saves = 0;
        context.SavingChanges += (_, _) => saves++;

        await CreateRepository(context).DeleteBoardAsync(Doomed, Token);

        saves.Should().Be(1);
    }

    [Fact]
    public async Task Delete_WhenTheSaveFails_ClearsTheTrackerAndRethrows()
    {
        var store = new Store();
        var seeded = await SeedQuestBoardAsync(store);

        await using (var context = store.NewContext())
        {
            context.SavingChanges += (_, _) => throw new InvalidOperationException("save failed");

            var act = () => CreateRepository(context).DeleteBoardAsync(Doomed, Token);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
            context.ChangeTracker.Entries().Should().BeEmpty();
        }

        await using var assertContext = store.NewContext();
        (await CountBoardRowsAsync(assertContext, Doomed)).Should().Contain(seeded.Counts);
        (await assertContext.Groups.CountAsync(g => g.Id == Doomed, Token)).Should().Be(1);
    }

    [Fact]
    public async Task IntegritySweep_FailsOnADanglingReference()
    {
        var store = new Store();
        await using var context = store.NewContext();
        await SeedBoardsAndUsersAsync(context, Doomed);
        context.Quests.Add(new QuestEntity
        {
            Title = "Orphan", Description = "d", GroupId = 9999, DungeonMasterId = DungeonMasterUserId
        });
        await context.SaveChangesAsync(Token);

        var act = () => AssertNoDanglingNonCascadeForeignKeys(context);

        var failure = await act.Should().ThrowAsync<XunitException>();
        failure.Which.Message.Should().Contain("QuestEntity.GroupId").And.Contain("9999");
    }

    private static async Task<SeededBoard> SeedFullTwoBoardStoreAsync(Store store)
    {
        await using var context = store.NewContext();
        await SeedBoardsAndUsersAsync(context, Doomed, Other);
        var doomed = await SeedFullBoardAsync(context, Doomed, "Doomed");
        await SeedFullBoardAsync(context, Other, "Other");
        await SeedSurvivorsAsync(context, Doomed, Other);
        return doomed;
    }

    // The in-memory store runs no database cascade, so character and contact image rows and the
    // contact-to-tag join rows are left orphaned here; their removal is proven by the model pin
    // and the SQL Server-gated deletion test instead of a row count in this suite.
    [Fact]
    public async Task Delete_RemovesEveryBoardScopedRowAndTheBoard()
    {
        var store = new Store();
        var seeded = await SeedFullTwoBoardStoreAsync(store);
        seeded.Counts.Keys.Should().BeEquivalentTo(Enum.GetValues<BoardContentKind>(), because: "the graph seeds every kind");

        BoardDeletionResult result;
        await using (var context = store.NewContext())
        {
            result = await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
        }

        result.Status.Should().Be(BoardDeleteStatus.Deleted);
        result.RemovedCounts.Should().BeEquivalentTo(seeded.Counts);

        await using var assertContext = store.NewContext();
        (await CountBoardRowsAsync(assertContext, Doomed)).Values.Should().OnlyContain(count => count == 0);
        (await assertContext.Groups.CountAsync(g => g.Id == Doomed, Token)).Should().Be(0);
        (await assertContext.CharacterClasses.IgnoreQueryFilters().CountAsync(Token)).Should().Be(1, because: "only the other board's character class remains");
        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_LeavesAnotherBoardUnchanged(bool otherBoardIsActive)
    {
        var store = new Store();
        await SeedFullTwoBoardStoreAsync(store);

        IReadOnlyList<string> before;
        IReadOnlyDictionary<BoardContentKind, int> countsBefore;
        await using (var context = store.NewContext())
        {
            before = await SnapshotBoardAsync(context, Other);
            countsBefore = await CountBoardRowsAsync(context, Other);
        }

        store.Groups.ActiveGroupId = otherBoardIsActive ? Other : null;
        await using (var context = store.NewContext())
        {
            await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
        }

        store.Groups.ActiveGroupId = null;
        await using var assertContext = store.NewContext();
        countsBefore.Values.Should().OnlyContain(count => count > 0, because: "the other board holds every kind");
        (await CountBoardRowsAsync(assertContext, Other)).Should().BeEquivalentTo(countsBefore);
        (await SnapshotBoardAsync(assertContext, Other)).Should().Equal(before);
        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }

    [Fact]
    public async Task Delete_LeavesUsersProfilesTradeItemsAndSubscriptionsUntouched()
    {
        var store = new Store();
        await SeedFullTwoBoardStoreAsync(store);

        SurvivorCounts before;
        await using (var context = store.NewContext())
        {
            before = await CountSurvivorsAsync(context);
        }

        await using (var context = store.NewContext())
        {
            await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
        }

        await using var assertContext = store.NewContext();
        before.Should().Be(new SurvivorCounts(3, 1, 1, 1, 1, 1));
        (await CountSurvivorsAsync(assertContext)).Should().Be(before);
        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }

    [Fact]
    public async Task Delete_UnlinksRowsOnOtherBoardsAndKeepsThem()
    {
        var store = new Store();
        await using (var seed = store.NewContext())
        {
            await SeedBoardsAndUsersAsync(seed, Doomed, Other);
        }

        SeededBoard doomed;
        ReachIns reachIns;
        await using (var seed = store.NewContext())
        {
            doomed = await SeedFullBoardAsync(seed, Doomed, "Doomed");
            reachIns = await SeedReachInsAsync(seed, doomed, Other);
        }

        await using (var context = store.NewContext())
        {
            var result = await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
            result.Status.Should().Be(BoardDeleteStatus.Deleted);
        }

        await using var assertContext = store.NewContext();
        var followUp = await assertContext.Quests.IgnoreQueryFilters().SingleAsync(q => q.Id == reachIns.FollowUpQuestId, Token);
        followUp.OriginalQuestId.Should().BeNull();
        followUp.GroupId.Should().Be(Other);

        var signup = await assertContext.PlayerSignups.IgnoreQueryFilters().SingleAsync(ps => ps.Id == reachIns.SignupId, Token);
        signup.CharacterId.Should().BeNull();
        signup.QuestId.Should().Be(reachIns.OtherQuestId);

        var otherEvent = await assertContext.Events.IgnoreQueryFilters().SingleAsync(e => e.Id == reachIns.EventId, Token);
        otherEvent.SeriesId.Should().BeNull();
        otherEvent.SeriesSlotIndex.Should().BeNull();
        otherEvent.GroupId.Should().Be(Other);

        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }

    [Fact]
    public async Task Delete_KeepsTheLinkedBoardSet()
    {
        var store = new Store();
        await SeedFullTwoBoardStoreAsync(store);

        int setId;
        await using (var context = store.NewContext())
        {
            setId = (await context.LinkedBoardSets.SingleAsync(Token)).Id;
        }

        await using (var context = store.NewContext())
        {
            await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
        }

        await using var assertContext = store.NewContext();
        (await assertContext.LinkedBoardSets.CountAsync(s => s.Id == setId, Token)).Should().Be(1);
        (await assertContext.Groups.SingleAsync(g => g.Id == Other, Token)).LinkedBoardSetId.Should().Be(setId);
        await AssertNoDanglingNonCascadeForeignKeys(assertContext);
    }
}
