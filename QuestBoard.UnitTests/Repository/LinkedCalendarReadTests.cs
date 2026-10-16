using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Repository;

public class LinkedCalendarReadTests
{
    private const int BoardA = 1;
    private const int BoardB = 2;
    private const int BoardC = 3;
    private const int ViewerId = 10;
    private const int SetId = 100;
    private const int OtherSetId = 101;

    private static readonly DateOnly From = new(2026, 11, 1);
    private static readonly DateOnly To = new(2026, 12, 1);

    private static string NewDatabaseName() => Guid.NewGuid().ToString();

    // The reading context is always active on board A, so any row of another board that comes
    // back got past the ambient filter through the bypass alone.
    private static QuestBoardContext CreateContext(string databaseName, int? activeGroupId = BoardA)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new TestActiveGroupContext(activeGroupId));
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static EventRepository CreateEventRepository(QuestBoardContext context) => new(context, CreateMapper());

    private static QuestRepository CreateQuestRepository(QuestBoardContext context) =>
        new(context, CreateMapper(), new FakeBoardClock { Today = From, Now = From.ToDateTime(TimeOnly.MinValue) });

    private static LinkedBoardSetRepository CreateSetRepository(QuestBoardContext context) => new(context);

    // Boards A and B share a set, board C sits in a different one. All three are one-shot boards.
    private static async Task<string> SeedBoardsAsync(
        BoardType boardType = BoardType.OneShot,
        Action<GroupEntity>? adjust = null)
    {
        var name = NewDatabaseName();
        await using var context = CreateContext(name);

        context.LinkedBoardSets.Add(new LinkedBoardSetEntity { Id = SetId, Name = "Set One", BoardType = (int)boardType, CreatedAt = DateTime.UtcNow });
        context.LinkedBoardSets.Add(new LinkedBoardSetEntity { Id = OtherSetId, Name = "Set Two", BoardType = (int)boardType, CreatedAt = DateTime.UtcNow });

        var a = new GroupEntity { Id = BoardA, Name = "Board A", BoardType = (int)boardType, LinkedBoardSetId = SetId };
        var b = new GroupEntity { Id = BoardB, Name = "Board B", BoardType = (int)boardType, LinkedBoardSetId = SetId };
        var c = new GroupEntity { Id = BoardC, Name = "Board C", BoardType = (int)boardType, LinkedBoardSetId = OtherSetId };
        adjust?.Invoke(a);
        adjust?.Invoke(b);
        adjust?.Invoke(c);
        context.Groups.AddRange(a, b, c);

        context.UserEntities.Add(new UserEntity { Id = ViewerId, Name = "Viewer", Email = "viewer@test.com", UserName = "viewer" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return name;
    }

    private static async Task SeedMembershipAsync(string databaseName, int groupId, GroupRole role)
    {
        await using var context = CreateContext(databaseName);
        context.UserGroups.Add(new UserGroupEntity { GroupId = groupId, UserId = ViewerId, GroupRole = (int)role });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedEventAsync(string databaseName, int id, int groupId, DateOnly date, TimeOnly? startTime = null, bool cancelled = false)
    {
        await using var context = CreateContext(databaseName);
        context.Events.Add(new EventEntity
        {
            Id = id,
            GroupId = groupId,
            Title = $"Event {id}",
            Date = date,
            StartTime = startTime,
            CancelledAt = cancelled ? DateTime.UtcNow : null
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------
    // Linked board lookup
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetLinkedBoardsForViewer_BoardInASetWithAnother_ReturnsTheOtherBoardWithViewerRole()
    {
        var database = await SeedBoardsAsync();
        await SeedMembershipAsync(database, BoardB, GroupRole.DungeonMaster);

        await using var context = CreateContext(database);
        var result = await CreateSetRepository(context).GetLinkedBoardsForViewerAsync(BoardA, ViewerId, TestContext.Current.CancellationToken);

        var board = Assert.Single(result.Boards);
        Assert.Equal(BoardB, board.Id);
        Assert.Equal("Board B", board.Name);
        Assert.Equal(GroupRole.DungeonMaster, board.ViewerRole);
        Assert.True(board.ViewerIsMember);
        Assert.Equal(BoardType.OneShot, result.SetBoardType);
    }

    [Fact]
    public async Task GetLinkedBoardsForViewer_ViewerIsNotAMemberOfTheOtherBoard_RoleIsNull()
    {
        var database = await SeedBoardsAsync();
        await SeedMembershipAsync(database, BoardA, GroupRole.Admin);

        await using var context = CreateContext(database);
        var result = await CreateSetRepository(context).GetLinkedBoardsForViewerAsync(BoardA, ViewerId, TestContext.Current.CancellationToken);

        var board = Assert.Single(result.Boards);
        Assert.Null(board.ViewerRole);
        Assert.False(board.ViewerIsMember);
    }

    [Fact]
    public async Task GetLinkedBoardsForViewer_ActiveBoardIsInNoSet_ReturnsNoBoards()
    {
        var database = await SeedBoardsAsync(adjust: g => g.LinkedBoardSetId = null);

        await using var context = CreateContext(database);
        var result = await CreateSetRepository(context).GetLinkedBoardsForViewerAsync(BoardA, ViewerId, TestContext.Current.CancellationToken);

        Assert.Empty(result.Boards);
    }

    [Fact]
    public async Task GetLinkedBoardsForViewer_ActiveBoardIsAloneInItsSet_ReturnsNoBoards()
    {
        var database = await SeedBoardsAsync();

        await using var context = CreateContext(database);
        var result = await CreateSetRepository(context).GetLinkedBoardsForViewerAsync(BoardC, ViewerId, TestContext.Current.CancellationToken);

        Assert.Empty(result.Boards);
    }

    [Fact]
    public async Task GetLinkedBoardsForViewer_OtherBoardTypeDiffersFromSet_LeavesThatBoardOut()
    {
        var database = await SeedBoardsAsync();
        await using (var seed = CreateContext(database))
        {
            var board = await seed.Groups.SingleAsync(g => g.Id == BoardB, TestContext.Current.CancellationToken);
            board.BoardType = (int)BoardType.Campaign;
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(database);
        var result = await CreateSetRepository(context).GetLinkedBoardsForViewerAsync(BoardA, ViewerId, TestContext.Current.CancellationToken);

        Assert.Empty(result.Boards);
    }

    [Fact]
    public async Task GetLinkedBoardsForViewer_ActiveBoardTypeDiffersFromSet_ReturnsNoBoards()
    {
        var database = await SeedBoardsAsync();
        await using (var seed = CreateContext(database))
        {
            var board = await seed.Groups.SingleAsync(g => g.Id == BoardA, TestContext.Current.CancellationToken);
            board.BoardType = (int)BoardType.Campaign;
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(database);
        var result = await CreateSetRepository(context).GetLinkedBoardsForViewerAsync(BoardA, ViewerId, TestContext.Current.CancellationToken);

        Assert.Empty(result.Boards);
    }

    // -------------------------------------------------------------------
    // Linked event read
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetLinkedEventsForCalendar_PinnedToBoardB_ReturnsOnlyBoardBEvents()
    {
        var database = await SeedBoardsAsync();
        var date = new DateOnly(2026, 11, 14);
        await SeedEventAsync(database, 1, BoardA, date);
        await SeedEventAsync(database, 2, BoardB, date, new TimeOnly(19, 0));
        await SeedEventAsync(database, 3, BoardC, date);

        await using var context = CreateContext(database);
        var result = await CreateEventRepository(context)
            .GetLinkedEventsForCalendarAsync([BoardB], From, To, TestContext.Current.CancellationToken);

        var entry = Assert.Single(result);
        Assert.Equal(2, entry.EventId);
        Assert.Equal(BoardB, entry.GroupId);
        Assert.Equal("Event 2", entry.Title);
        Assert.Equal(date, entry.Date);
        Assert.Equal(new TimeOnly(19, 0), entry.StartTime);
        Assert.False(entry.IsCancelled);
    }

    [Fact]
    public async Task GetLinkedEventsForCalendar_EventAtUpperBoundExcludedAndAtLowerBoundIncluded()
    {
        var database = await SeedBoardsAsync();
        await SeedEventAsync(database, 1, BoardB, From);
        await SeedEventAsync(database, 2, BoardB, To);
        await SeedEventAsync(database, 3, BoardB, From.AddDays(-1));

        await using var context = CreateContext(database);
        var result = await CreateEventRepository(context)
            .GetLinkedEventsForCalendarAsync([BoardB], From, To, TestContext.Current.CancellationToken);

        var entry = Assert.Single(result);
        Assert.Equal(1, entry.EventId);
    }

    [Fact]
    public async Task GetLinkedEventsForCalendar_CancelledEvent_IsReturnedAndFlagged()
    {
        var database = await SeedBoardsAsync();
        await SeedEventAsync(database, 1, BoardB, new DateOnly(2026, 11, 10), cancelled: true);

        await using var context = CreateContext(database);
        var result = await CreateEventRepository(context)
            .GetLinkedEventsForCalendarAsync([BoardB], From, To, TestContext.Current.CancellationToken);

        var entry = Assert.Single(result);
        Assert.True(entry.IsCancelled);
    }

    [Fact]
    public async Task GetLinkedEventsForCalendar_EmptyIdCollection_ReturnsEmptyList()
    {
        var database = await SeedBoardsAsync();
        await SeedEventAsync(database, 1, BoardB, new DateOnly(2026, 11, 10));

        await using var context = CreateContext(database);
        var result = await CreateEventRepository(context)
            .GetLinkedEventsForCalendarAsync([], From, To, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    private sealed class TestActiveGroupContext(int? activeGroupId) : IActiveGroupContext
    {
        public int? ActiveGroupId { get; } = activeGroupId;
    }
}
