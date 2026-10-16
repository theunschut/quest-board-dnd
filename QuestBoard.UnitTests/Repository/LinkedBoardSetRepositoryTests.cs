using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

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

    private sealed class TestActiveGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => null;
    }
}
