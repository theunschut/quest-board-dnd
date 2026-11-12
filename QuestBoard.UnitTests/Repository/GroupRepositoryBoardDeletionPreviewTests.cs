using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.Repository.Entities;
using static QuestBoard.UnitTests.Repository.BoardDeletionTestGraph;

namespace QuestBoard.UnitTests.Repository;

// Proves the deletion preview against the in-memory store. The preview is built from the same
// scoped queries the delete removes rows with, so the central fact is that the two agree.
public class GroupRepositoryBoardDeletionPreviewTests
{
    private const int Doomed = 10;
    private const int Other = 20;

    private static readonly BoardContentKind[] CountOnlyKinds =
    [
        BoardContentKind.ProposedDates,
        BoardContentKind.DateVotes,
        BoardContentKind.PlayerSignups,
        BoardContentKind.ReminderLogs,
        BoardContentKind.EventSignups,
        BoardContentKind.ContactNotes,
        BoardContentKind.Transactions
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SeededBoard> SeedTwoFullBoardsAsync(Store store)
    {
        await using var context = store.NewContext();
        await SeedBoardsAndUsersAsync(context, Doomed, Other);
        var doomed = await SeedFullBoardAsync(context, Doomed, "Doomed");
        await SeedFullBoardAsync(context, Other, "Other");
        return doomed;
    }

    private static async Task<BoardDeletionPreview> PreviewAsync(Store store, int groupId = Doomed)
    {
        await using var context = store.NewContext();
        return await CreateRepository(context).GetDeletionPreviewAsync(groupId, Token);
    }

    [Fact]
    public async Task Preview_CountsEqualWhatTheDeleteRemoves()
    {
        var store = new Store();
        var seeded = await SeedTwoFullBoardsAsync(store);

        var preview = await PreviewAsync(store);

        preview.Items.Select(i => i.Kind).Should().Equal(Enum.GetValues<BoardContentKind>(), because: "every kind is listed in the page order");

        BoardDeletionResult result;
        await using (var context = store.NewContext())
        {
            result = await CreateRepository(context).DeleteBoardAsync(Doomed, Token);
        }

        result.Status.Should().Be(BoardDeleteStatus.Deleted);
        foreach (var item in preview.Items)
        {
            item.Count.Should().Be(result.RemovedCounts[item.Kind], because: $"{item.Kind} is previewed from the delete's own query");
            item.Count.Should().Be(seeded.Counts[item.Kind]);
        }
    }

    [Fact]
    public async Task Preview_ListsNamesOnlyForNamedKindsSortedIgnoringCase()
    {
        var store = new Store();
        await SeedTwoFullBoardsAsync(store);
        await using (var context = store.NewContext())
        {
            foreach (var title in new[] { "zebra", "Banana", "apple", "Apple", "apple" })
            {
                context.Quests.Add(new QuestEntity
                {
                    Title = title, Description = "d", GroupId = Doomed, DungeonMasterId = DungeonMasterUserId
                });
            }

            await context.SaveChangesAsync(Token);
        }

        var preview = await PreviewAsync(store);

        foreach (var item in preview.Items)
        {
            if (CountOnlyKinds.Contains(item.Kind))
            {
                item.Names.Should().BeNull(because: $"{item.Kind} is only counted");
            }
            else
            {
                item.Names.Should().NotBeNull(because: $"{item.Kind} lists its rows");
                item.Names!.Count.Should().Be(item.Count);
                item.Names.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
                item.Names.Should().OnlyContain(name => !name.StartsWith("Other"), because: "another board's rows are never listed");
            }
        }

        var quests = preview.Items.Single(i => i.Kind == BoardContentKind.Quests);
        quests.Names!.Where(n => n.Equals("apple", StringComparison.OrdinalIgnoreCase))
            .Should().BeEquivalentTo(["Apple", "apple", "apple"], because: "duplicates are kept");
        quests.Names!.Take(4).Select(n => n.ToLowerInvariant()).Should().Equal("apple", "apple", "apple", "banana");
        quests.Names!.Last().Should().Be("zebra");
    }

    [Fact]
    public async Task Preview_KeepsEmptyKindsWithZero()
    {
        var store = new Store();
        await using (var context = store.NewContext())
        {
            await SeedBoardsAndUsersAsync(context, Doomed, Other);
        }

        var preview = await PreviewAsync(store);

        preview.Items.Should().HaveCount(Enum.GetValues<BoardContentKind>().Length);
        preview.Items.Should().OnlyContain(i => i.Count == 0);
        preview.Items.Where(i => !CountOnlyKinds.Contains(i.Kind)).Should().OnlyContain(i => i.Names != null && i.Names.Count == 0);
        preview.FollowUpsOnOtherBoards.Should().BeEmpty();
        preview.SignupsLosingCharacter.Should().Be(0);
        preview.EventsLeavingSeries.Should().Be(0);
    }

    [Fact]
    public async Task Preview_ReportsRowsOnOtherBoardsSeparately()
    {
        var store = new Store();
        await using (var seed = store.NewContext())
        {
            await SeedBoardsAndUsersAsync(seed, Doomed, Other);
        }

        await using (var seed = store.NewContext())
        {
            var doomed = await SeedFullBoardAsync(seed, Doomed, "Doomed");
            await SeedReachInsAsync(seed, doomed, Other);
            seed.Quests.Add(new QuestEntity
            {
                Title = "Another follow-up", Description = "d", GroupId = Other,
                DungeonMasterId = DungeonMasterUserId, OriginalQuestId = doomed.OriginalQuestId
            });
            await seed.SaveChangesAsync(Token);
        }

        var preview = await PreviewAsync(store);

        preview.FollowUpsOnOtherBoards.Select(f => f.Title).Should().Equal("Another follow-up", "Reach-in follow-up");
        preview.FollowUpsOnOtherBoards.Should().OnlyContain(f => f.BoardName == $"Board {Other}");
        preview.SignupsLosingCharacter.Should().Be(1);
        preview.EventsLeavingSeries.Should().Be(1);

        var quests = preview.Items.Single(i => i.Kind == BoardContentKind.Quests);
        quests.Names.Should().NotContain(["Another follow-up", "Reach-in follow-up", "Reach-in host"], because: "those rows stay on their own board");
        quests.Count.Should().Be(2);
        preview.Items.Single(i => i.Kind == BoardContentKind.PlayerSignups).Count.Should().Be(2);
        preview.Items.Single(i => i.Kind == BoardContentKind.Events).Count.Should().Be(2);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(null)]
    public async Task Preview_DoesNotDependOnTheActiveBoard(int? activeGroupId)
    {
        var store = new Store();
        await using (var seed = store.NewContext())
        {
            await SeedBoardsAndUsersAsync(seed, Doomed, Other);
            var doomed = await SeedFullBoardAsync(seed, Doomed, "Doomed");
            await SeedFullBoardAsync(seed, Other, "Other");
            await SeedReachInsAsync(seed, doomed, Other);
        }

        store.Groups.ActiveGroupId = Doomed;
        var reference = await PreviewAsync(store);

        store.Groups.ActiveGroupId = activeGroupId;
        var preview = await PreviewAsync(store);

        preview.Should().BeEquivalentTo(reference, options => options.WithStrictOrdering());
        preview.Items.Single(i => i.Kind == BoardContentKind.Quests).Count.Should().BeGreaterThan(0);
        preview.FollowUpsOnOtherBoards.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Preview_ChangesNothing()
    {
        var store = new Store();
        await SeedTwoFullBoardsAsync(store);

        IReadOnlyDictionary<BoardContentKind, int> before;
        IReadOnlyList<string> snapshotBefore;
        await using (var context = store.NewContext())
        {
            before = await CountBoardRowsAsync(context, Doomed);
            snapshotBefore = await SnapshotBoardAsync(context, Doomed);
        }

        await using (var context = store.NewContext())
        {
            var saves = 0;
            context.SavingChanges += (_, _) => saves++;

            await CreateRepository(context).GetDeletionPreviewAsync(Doomed, Token);

            saves.Should().Be(0);
            context.ChangeTracker.HasChanges().Should().BeFalse();
        }

        await using var assertContext = store.NewContext();
        (await CountBoardRowsAsync(assertContext, Doomed)).Should().BeEquivalentTo(before);
        (await SnapshotBoardAsync(assertContext, Doomed)).Should().Equal(snapshotBefore);
        (await assertContext.Groups.CountAsync(g => g.Id == Doomed, Token)).Should().Be(1);
    }
}
