using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class UserRepositoryBoardNamesTests
{
    private static QuestBoardContext CreateContext(string databaseName, StubGroupContext groupContext)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, groupContext);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static async Task SeedAsync(QuestBoardContext context)
    {
        context.Groups.Add(new GroupEntity { Id = 1, Name = "gamma" });
        context.Groups.Add(new GroupEntity { Id = 2, Name = "Beta" });
        context.Groups.Add(new GroupEntity { Id = 3, Name = "alpha" });
        context.Groups.Add(new GroupEntity { Id = 4, Name = "Beta" });

        context.UserEntities.Add(new UserEntity { Id = 10, Name = "Many", Email = "many@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 11, Name = "Dupes", Email = "dupes@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 12, Name = "None", Email = "none@test.com" });

        context.UserGroups.Add(new UserGroupEntity { UserId = 10, GroupId = 1 });
        context.UserGroups.Add(new UserGroupEntity { UserId = 10, GroupId = 2 });
        context.UserGroups.Add(new UserGroupEntity { UserId = 10, GroupId = 3 });
        context.UserGroups.Add(new UserGroupEntity { UserId = 11, GroupId = 2 });
        context.UserGroups.Add(new UserGroupEntity { UserId = 11, GroupId = 4 });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetBoardNamesByUserAsync_SortsNamesAToZIgnoringCase()
    {
        await using var context = CreateContext(nameof(GetBoardNamesByUserAsync_SortsNamesAToZIgnoringCase), new StubGroupContext());
        await SeedAsync(context);
        var repository = new UserRepository(context, CreateMapper(), new StubGroupContext());

        var result = await repository.GetBoardNamesByUserAsync(TestContext.Current.CancellationToken);

        result[10].Should().Equal("alpha", "Beta", "gamma");
    }

    [Fact]
    public async Task GetBoardNamesByUserAsync_UserOnNoBoard_IsAbsent()
    {
        await using var context = CreateContext(nameof(GetBoardNamesByUserAsync_UserOnNoBoard_IsAbsent), new StubGroupContext());
        await SeedAsync(context);
        var repository = new UserRepository(context, CreateMapper(), new StubGroupContext());

        var result = await repository.GetBoardNamesByUserAsync(TestContext.Current.CancellationToken);

        result.Should().NotContainKey(12);
        result.Keys.Should().BeEquivalentTo([10, 11]);
    }

    [Fact]
    public async Task GetBoardNamesByUserAsync_DuplicateBoardNamesOnDifferentBoards_BothAppear()
    {
        await using var context = CreateContext(nameof(GetBoardNamesByUserAsync_DuplicateBoardNamesOnDifferentBoards_BothAppear), new StubGroupContext());
        await SeedAsync(context);
        var repository = new UserRepository(context, CreateMapper(), new StubGroupContext());

        var result = await repository.GetBoardNamesByUserAsync(TestContext.Current.CancellationToken);

        result[11].Should().Equal("Beta", "Beta");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(99)]
    public async Task GetBoardNamesByUserAsync_DoesNotDependOnTheActiveBoard(int? activeGroupId)
    {
        var databaseName = $"{nameof(GetBoardNamesByUserAsync_DoesNotDependOnTheActiveBoard)}-{activeGroupId}";
        await using var context = CreateContext(databaseName, new StubGroupContext());
        await SeedAsync(context);
        var repository = new UserRepository(context, CreateMapper(), new StubGroupContext { ActiveGroupId = activeGroupId });

        var result = await repository.GetBoardNamesByUserAsync(TestContext.Current.CancellationToken);

        result[10].Should().Equal("alpha", "Beta", "gamma");
        result[11].Should().Equal("Beta", "Beta");
        result.Should().HaveCount(2);
    }

    private sealed class StubGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
