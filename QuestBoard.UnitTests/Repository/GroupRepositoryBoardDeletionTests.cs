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
}
