using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Repository;

public class LinkedBoardSetRepositoryTests
{
    private static QuestBoardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new TestActiveGroupContext());
    }

    private static LinkedBoardSetRepository CreateRepository(QuestBoardContext context) => new(context);

    private static async Task<GroupEntity> SeedBoardAsync(
        QuestBoardContext context, int groupId, string name, BoardType boardType = BoardType.OneShot)
    {
        var group = new GroupEntity { Id = groupId, Name = name, BoardType = (int)boardType };
        context.Groups.Add(group);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return group;
    }

    [Fact]
    public async Task CreateAsync_ThenAssigningTwoBoards_StoresTheLinkAndReadsItBackInNameOrder()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        await SeedBoardAsync(context, 1, "Zeta Table");
        await SeedBoardAsync(context, 2, "Alpha Table");
        var repository = CreateRepository(context);

        // Act
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);
        var firstAssigned = await repository.AddBoardAsync(setId!.Value, 1, TestContext.Current.CancellationToken);
        var secondAssigned = await repository.AddBoardAsync(setId!.Value, 2, TestContext.Current.CancellationToken);

        // Assert
        setId.Should().NotBeNull();
        firstAssigned.Should().Be(LinkedBoardSetResult.Success);
        secondAssigned.Should().Be(LinkedBoardSetResult.Success);

        var rows = await context.Groups.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().OnlyContain(g => g.LinkedBoardSetId == setId);

        var sets = await repository.GetAllAsync(TestContext.Current.CancellationToken);
        var set = sets.Should().ContainSingle().Subject;
        set.Name.Should().Be("Friday Group");
        set.BoardType.Should().Be(BoardType.OneShot);
        set.Boards.Select(b => b.Name).Should().Equal("Alpha Table", "Zeta Table");
        set.IsActive.Should().BeTrue();

        var byId = await repository.GetByIdAsync(setId!.Value, TestContext.Current.CancellationToken);
        byId.Should().NotBeNull();
        byId!.Id.Should().Be(set.Id);
        byId.Boards.Select(b => b.Name).Should().Equal("Alpha Table", "Zeta Table");
    }

    [Fact]
    public async Task AddBoardAsync_UnknownBoard_ReturnsNotFound()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Act
        var found = await repository.AddBoardAsync(setId!.Value, 999, TestContext.Current.CancellationToken);

        // Assert
        found.Should().Be(LinkedBoardSetResult.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_UnlinksEveryBoardAndRemovesOnlyTheSet()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        await SeedBoardAsync(context, 1, "Alpha Table");
        await SeedBoardAsync(context, 2, "Beta Table");
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);
        await repository.AddBoardAsync(setId!.Value, 1, TestContext.Current.CancellationToken);
        await repository.AddBoardAsync(setId!.Value, 2, TestContext.Current.CancellationToken);

        // Act
        var deleted = await repository.DeleteAsync(setId!.Value, TestContext.Current.CancellationToken);

        // Assert
        deleted.Should().BeTrue();
        (await context.LinkedBoardSets.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        var boards = await context.Groups.AsNoTracking().OrderBy(g => g.Id).ToListAsync(TestContext.Current.CancellationToken);
        boards.Should().HaveCount(2);
        boards.Should().OnlyContain(g => g.LinkedBoardSetId == null);
    }

    [Fact]
    public async Task DeleteAsync_UnknownSet_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = CreateRepository(context);

        // Act
        var deleted = await repository.DeleteAsync(999, TestContext.Current.CancellationToken);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task RemovingOneBoardRow_LeavesTheSetAndTheOtherBoardsLinked()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var doomed = await SeedBoardAsync(context, 1, "Alpha Table");
        await SeedBoardAsync(context, 2, "Beta Table");
        await SeedBoardAsync(context, 3, "Gamma Table");
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);
        foreach (var boardId in new[] { 1, 2, 3 })
            await repository.AddBoardAsync(setId!.Value, boardId, TestContext.Current.CancellationToken);

        // Act
        context.Groups.Remove(doomed);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var sets = await repository.GetAllAsync(TestContext.Current.CancellationToken);
        var set = sets.Should().ContainSingle().Subject;
        set.Boards.Select(b => b.Id).Should().Equal(2, 3);
        var remaining = await context.Groups.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        remaining.Should().OnlyContain(g => g.LinkedBoardSetId == setId);
    }

    [Fact]
    public async Task GetUnlinkedBoardsAsync_ExcludesBoardsInASet_AndOrdersByName()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        await SeedBoardAsync(context, 1, "Zeta Table");
        await SeedBoardAsync(context, 2, "Alpha Table");
        await SeedBoardAsync(context, 3, "Linked Table");
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);
        await repository.AddBoardAsync(setId!.Value, 3, TestContext.Current.CancellationToken);

        // Act
        var unlinked = await repository.GetUnlinkedBoardsAsync(TestContext.Current.CancellationToken);

        // Assert
        unlinked.Select(b => b.Name).Should().Equal("Alpha Table", "Zeta Table");
        unlinked.Should().OnlyContain(b => b.LinkedBoardSetId == null);
    }

    [Fact]
    public async Task GetAllAsync_OrdersSetsByName_AndKeepsSetsWithOneBoardOrNone()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        await SeedBoardAsync(context, 1, "Alpha Table");
        var repository = CreateRepository(context);
        var lonelyId = await repository.CreateAsync("Beta Set", BoardType.OneShot, TestContext.Current.CancellationToken);
        await repository.CreateAsync("Alpha Set", BoardType.Campaign, TestContext.Current.CancellationToken);
        await repository.AddBoardAsync(lonelyId!.Value, 1, TestContext.Current.CancellationToken);

        // Act
        var sets = await repository.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        sets.Select(s => s.Name).Should().Equal("Alpha Set", "Beta Set");
        sets.Should().OnlyContain(s => !s.IsActive);
        sets[0].Boards.Should().BeEmpty();
        sets[1].Boards.Should().ContainSingle();
    }

    [Fact]
    public async Task GetBoardAsync_ReturnsNameTypeAndSet_OrNullForUnknownBoard()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        await SeedBoardAsync(context, 1, "Alpha Table", BoardType.Campaign);
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.Campaign, TestContext.Current.CancellationToken);
        await repository.AddBoardAsync(setId!.Value, 1, TestContext.Current.CancellationToken);

        // Act
        var board = await repository.GetBoardAsync(1, TestContext.Current.CancellationToken);
        var missing = await repository.GetBoardAsync(999, TestContext.Current.CancellationToken);

        // Assert
        board.Should().NotBeNull();
        board!.Name.Should().Be("Alpha Table");
        board.BoardType.Should().Be(BoardType.Campaign);
        board.LinkedBoardSetId.Should().Be(setId);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task NameExistsAsync_MatchesIgnoringCase_AndCanExcludeOneSet()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Act
        var differsInCase = await repository.NameExistsAsync("FRIDAY group", null, TestContext.Current.CancellationToken);
        var onlyMatchIsExcluded = await repository.NameExistsAsync("Friday Group", setId, TestContext.Current.CancellationToken);
        var unknownName = await repository.NameExistsAsync("Saturday Group", null, TestContext.Current.CancellationToken);

        // Assert
        differsInCase.Should().BeTrue();
        onlyMatchIsExcluded.Should().BeFalse();
        unknownName.Should().BeFalse();
    }

    [Fact]
    public async Task RenameAsync_RenamesAnExistingSet_AndReturnsNotFoundForAnUnknownOne()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Act
        var renamed = await repository.RenameAsync(setId!.Value, "Weekend Group", TestContext.Current.CancellationToken);
        var unknown = await repository.RenameAsync(999, "Anything", TestContext.Current.CancellationToken);

        // Assert
        renamed.Should().Be(LinkedBoardSetResult.Success);
        unknown.Should().Be(LinkedBoardSetResult.NotFound);
        var set = await repository.GetByIdAsync(setId.Value, TestContext.Current.CancellationToken);
        set!.Name.Should().Be("Weekend Group");
    }

    [Fact]
    public async Task RenamingALinkedBoardThroughTheGroupRepository_KeepsItsLink()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        await SeedBoardAsync(context, 1, "Alpha Table");
        var setRepository = CreateRepository(context);
        var setId = await setRepository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);
        await setRepository.AddBoardAsync(setId!.Value, 1, TestContext.Current.CancellationToken);

        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var groupRepository = new GroupRepository(context, mapper, new FakeBoardClock());

        // Act
        await groupRepository.UpdateAsync(
            new Group { Id = 1, Name = "Renamed", BoardType = BoardType.OneShot },
            TestContext.Current.CancellationToken);

        // Assert
        var row = await context.Groups.AsNoTracking().SingleAsync(g => g.Id == 1, TestContext.Current.CancellationToken);
        row.Name.Should().Be("Renamed");
        row.LinkedBoardSetId.Should().Be(setId);
    }

    // ---------------------------------------------------------------------------
    // Writes that race another super admin, or that the database refuses
    // ---------------------------------------------------------------------------

    // Two contexts over one store stand for two requests. The interceptor runs the other request's
    // change just before this request's save lands, which is the window a pre-check cannot close.
    private sealed class RaceStore
    {
        private readonly InMemoryDatabaseRoot root = new();
        private readonly string name = Guid.NewGuid().ToString();

        public InterruptedSave Interceptor { get; } = new();

        public QuestBoardContext ThisRequest() => new(
            new DbContextOptionsBuilder<QuestBoardContext>()
                .UseInMemoryDatabase(name, root)
                .AddInterceptors(Interceptor)
                .Options,
            new TestActiveGroupContext());

        public QuestBoardContext OtherRequest() => new(
            new DbContextOptionsBuilder<QuestBoardContext>()
                .UseInMemoryDatabase(name, root)
                .Options,
            new TestActiveGroupContext());

        public async Task<int> SeedSetAsync(string setName)
        {
            await using var context = OtherRequest();
            var set = new LinkedBoardSetEntity { Name = setName, BoardType = (int)BoardType.OneShot };
            context.LinkedBoardSets.Add(set);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return set.Id;
        }

        public async Task SeedBoardAsync(int groupId, int? setId = null)
        {
            await using var context = OtherRequest();
            context.Groups.Add(new GroupEntity
            {
                Id = groupId, Name = $"Board {groupId}", BoardType = (int)BoardType.OneShot, LinkedBoardSetId = setId
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task InOtherRequestAsync(Func<QuestBoardContext, Task> change)
        {
            await using var context = OtherRequest();
            await change(context);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task<int?> StoredSetOfAsync(int groupId)
        {
            await using var context = OtherRequest();
            return await context.Groups.Where(g => g.Id == groupId).Select(g => g.LinkedBoardSetId)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // Runs one armed action as the next save begins. It can then refuse the save the way SQL Server
    // would for a unique index or a foreign key, which the in-memory provider does not enforce.
    private sealed class InterruptedSave : SaveChangesInterceptor
    {
        private Func<Task>? beforeSave;
        private bool refuse;

        public void Arm(Func<Task> action, bool thenRefuse = false)
        {
            beforeSave = action;
            refuse = thenRefuse;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var action = beforeSave;
            beforeSave = null;
            if (action != null)
            {
                await action();
                if (refuse) throw new DbUpdateException("The database refused the save.");
            }

            return result;
        }
    }

    [Fact]
    public async Task CreateAsync_WhenAnotherSetTakesTheNameBeforeTheSave_ReturnsNullAndKeepsOnlyTheirSet()
    {
        // Arrange
        var store = new RaceStore();
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.SeedSetAsync("Friday Group"), thenRefuse: true);

        // Act
        var setId = await CreateRepository(context).CreateAsync("friday group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Assert
        setId.Should().BeNull();
        await using var check = store.OtherRequest();
        (await check.LinkedBoardSets.Select(s => s.Name).ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal("Friday Group");
    }

    [Fact]
    public async Task CreateAsync_WhenTheSaveFailsAndTheNameIsFree_ThrowsInsteadOfReportingADuplicate()
    {
        // Arrange
        var store = new RaceStore();
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => Task.CompletedTask, thenRefuse: true);

        // Act
        var act = () => CreateRepository(context).CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RenameAsync_WhenTheSetIsDeletedBeforeTheSave_ReturnsNotFoundWithoutThrowing()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.InOtherRequestAsync(async other =>
            other.LinkedBoardSets.Remove(await other.LinkedBoardSets.SingleAsync(TestContext.Current.CancellationToken))));

        // Act
        var result = await CreateRepository(context).RenameAsync(setId, "Weekend Group", TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.NotFound);
    }

    [Fact]
    public async Task RenameAsync_WhenAnotherSetTakesTheNameBeforeTheSave_ReturnsDuplicateNameAndKeepsTheStoredName()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.SeedSetAsync("Weekend Group"), thenRefuse: true);
        var repository = CreateRepository(context);

        // Act
        var result = await repository.RenameAsync(setId, "Weekend Group", TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.DuplicateName);
        (await context.LinkedBoardSets.SingleAsync(s => s.Id == setId, TestContext.Current.CancellationToken))
            .Name.Should().Be("Friday Group");
    }

    [Fact]
    public async Task RenameAsync_WhenTheSaveFailsAndTheNameIsFree_ThrowsInsteadOfReportingADuplicate()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => Task.CompletedTask, thenRefuse: true);

        // Act
        var act = () => CreateRepository(context).RenameAsync(setId, "Weekend Group", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AddBoardAsync_WhenAnotherAdminLinksTheBoardFirst_ReturnsAlreadyLinkedAndLeavesItInTheirSet()
    {
        // Arrange
        var store = new RaceStore();
        var ourSet = await store.SeedSetAsync("Friday Group");
        var theirSet = await store.SeedSetAsync("Saturday Group");
        await store.SeedBoardAsync(9);
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.InOtherRequestAsync(async other =>
            (await other.Groups.SingleAsync(g => g.Id == 9, TestContext.Current.CancellationToken)).LinkedBoardSetId = theirSet));

        // Act
        var result = await CreateRepository(context).AddBoardAsync(ourSet, 9, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.AlreadyLinked);
        (await store.StoredSetOfAsync(9)).Should().Be(theirSet);
    }

    [Fact]
    public async Task AddBoardAsync_WhenTheBoardIsAlreadyInASet_ReturnsAlreadyLinkedAndWritesNothing()
    {
        // Arrange
        var store = new RaceStore();
        var ourSet = await store.SeedSetAsync("Friday Group");
        var theirSet = await store.SeedSetAsync("Saturday Group");
        await store.SeedBoardAsync(9, theirSet);
        await using var context = store.ThisRequest();

        // Act
        var result = await CreateRepository(context).AddBoardAsync(ourSet, 9, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.AlreadyLinked);
        (await store.StoredSetOfAsync(9)).Should().Be(theirSet);
    }

    [Fact]
    public async Task AddBoardAsync_WhenTheSetIsDeletedBeforeTheSave_ReturnsNotFoundInsteadOfFailing()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await store.SeedBoardAsync(9);
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.InOtherRequestAsync(async other =>
            other.LinkedBoardSets.Remove(await other.LinkedBoardSets.SingleAsync(TestContext.Current.CancellationToken))),
            thenRefuse: true);

        // Act
        var result = await CreateRepository(context).AddBoardAsync(setId, 9, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.NotFound);
    }

    [Fact]
    public async Task AddBoardAsync_WhenTheSaveFailsAndTheSetStillExists_Throws()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await store.SeedBoardAsync(9);
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => Task.CompletedTask, thenRefuse: true);

        // Act
        var act = () => CreateRepository(context).AddBoardAsync(setId, 9, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RemoveBoardAsync_TakesTheBoardOutOfTheSet()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await store.SeedBoardAsync(9, setId);
        await using var context = store.ThisRequest();

        // Act
        var result = await CreateRepository(context).RemoveBoardAsync(setId, 9, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.Success);
        (await store.StoredSetOfAsync(9)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveBoardAsync_WhenAnotherAdminMovesTheBoardFirst_ReturnsNotInSetAndLeavesItWhereTheyPutIt()
    {
        // Arrange
        var store = new RaceStore();
        var ourSet = await store.SeedSetAsync("Friday Group");
        var theirSet = await store.SeedSetAsync("Saturday Group");
        await store.SeedBoardAsync(9, ourSet);
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.InOtherRequestAsync(async other =>
            (await other.Groups.SingleAsync(g => g.Id == 9, TestContext.Current.CancellationToken)).LinkedBoardSetId = theirSet));

        // Act
        var result = await CreateRepository(context).RemoveBoardAsync(ourSet, 9, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(LinkedBoardSetResult.NotInSet);
        (await store.StoredSetOfAsync(9)).Should().Be(theirSet);
    }

    [Fact]
    public async Task RemoveBoardAsync_WhenTheBoardIsInAnotherSetOrMissing_WritesNothing()
    {
        // Arrange
        var store = new RaceStore();
        var ourSet = await store.SeedSetAsync("Friday Group");
        var theirSet = await store.SeedSetAsync("Saturday Group");
        await store.SeedBoardAsync(9, theirSet);
        await using var context = store.ThisRequest();
        var repository = CreateRepository(context);

        // Act
        var otherSet = await repository.RemoveBoardAsync(ourSet, 9, TestContext.Current.CancellationToken);
        var missing = await repository.RemoveBoardAsync(ourSet, 404, TestContext.Current.CancellationToken);

        // Assert
        otherSet.Should().Be(LinkedBoardSetResult.NotInSet);
        missing.Should().Be(LinkedBoardSetResult.NotFound);
        (await store.StoredSetOfAsync(9)).Should().Be(theirSet);
    }

    [Fact]
    public async Task DeleteAsync_WhenAnotherAdminDeletedTheSetFirst_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var store = new RaceStore();
        var setId = await store.SeedSetAsync("Friday Group");
        await using var context = store.ThisRequest();
        store.Interceptor.Arm(() => store.InOtherRequestAsync(async other =>
            other.LinkedBoardSets.Remove(await other.LinkedBoardSets.SingleAsync(TestContext.Current.CancellationToken))));

        // Act
        var deleted = await CreateRepository(context).DeleteAsync(setId, TestContext.Current.CancellationToken);

        // Assert
        deleted.Should().BeFalse();
    }

    private sealed class TestActiveGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => null;
    }
}
