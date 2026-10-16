using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
        var firstAssigned = await repository.AssignBoardAsync(1, setId, TestContext.Current.CancellationToken);
        var secondAssigned = await repository.AssignBoardAsync(2, setId, TestContext.Current.CancellationToken);

        // Assert
        setId.Should().NotBeNull();
        firstAssigned.Should().BeTrue();
        secondAssigned.Should().BeTrue();

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
    public async Task AssignBoardAsync_UnknownBoard_ReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Act
        var found = await repository.AssignBoardAsync(999, setId, TestContext.Current.CancellationToken);

        // Assert
        found.Should().BeFalse();
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
        await repository.AssignBoardAsync(1, setId, TestContext.Current.CancellationToken);
        await repository.AssignBoardAsync(2, setId, TestContext.Current.CancellationToken);

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
            await repository.AssignBoardAsync(boardId, setId, TestContext.Current.CancellationToken);

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
        await repository.AssignBoardAsync(3, setId, TestContext.Current.CancellationToken);

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
        await repository.AssignBoardAsync(1, lonelyId, TestContext.Current.CancellationToken);

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
        await repository.AssignBoardAsync(1, setId, TestContext.Current.CancellationToken);

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
    public async Task RenameAsync_RenamesAnExistingSet_AndReturnsFalseForAnUnknownOne()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var repository = CreateRepository(context);
        var setId = await repository.CreateAsync("Friday Group", BoardType.OneShot, TestContext.Current.CancellationToken);

        // Act
        var renamed = await repository.RenameAsync(setId!.Value, "Weekend Group", TestContext.Current.CancellationToken);
        var unknown = await repository.RenameAsync(999, "Anything", TestContext.Current.CancellationToken);

        // Assert
        renamed.Should().BeTrue();
        unknown.Should().BeFalse();
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
        await setRepository.AssignBoardAsync(1, setId, TestContext.Current.CancellationToken);

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

    private sealed class TestActiveGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => null;
    }
}
