using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class BoardPageCountQueryTests
{
    private const int BoardOne = 1;
    private const int BoardTwo = 2;

    private static readonly DateOnly Today = new(2031, 6, 15);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The reading context is always active on board two, so any count that includes board one's
    // rows got there through the explicit group id alone.
    private static QuestBoardContext CreateContext(string databaseName, int? activeGroupId = BoardTwo)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new TestActiveGroupContext(activeGroupId));
    }

    private static EventRepository CreateRepository(QuestBoardContext context)
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return new EventRepository(context, configuration.CreateMapper());
    }

    private static async Task<string> SeedAsync(Action<QuestBoardContext> seed)
    {
        var name = Guid.NewGuid().ToString();
        await using var context = CreateContext(name);
        context.Groups.AddRange(
            new GroupEntity { Id = BoardOne, Name = "Board One", BoardType = (int)BoardType.OneShot },
            new GroupEntity { Id = BoardTwo, Name = "Board Two", BoardType = (int)BoardType.OneShot });
        seed(context);
        await context.SaveChangesAsync(Token);
        return name;
    }

    private static EventEntity Event(int id, int groupId, DateOnly date, bool cancelled = false) => new()
    {
        Id = id,
        GroupId = groupId,
        Title = $"Event {id}",
        Date = date,
        CancelledAt = cancelled ? DateTime.UtcNow : null
    };

    private static EventSeriesEntity Series(int id, int groupId, DateOnly? endDate) => new()
    {
        Id = id,
        GroupId = groupId,
        Title = $"Series {id}",
        AnchorDate = Today.AddDays(-30),
        IntervalWeeks = 1,
        CycleMask = "1",
        EndDate = endDate
    };

    [Fact]
    public async Task CountUpcomingForBoard_TodayOnwardsIncludingCancelled_ExcludesPastAndOtherBoards()
    {
        var database = await SeedAsync(context => context.Events.AddRange(
            Event(1, BoardOne, Today.AddDays(-1)),
            Event(2, BoardOne, Today),
            Event(3, BoardOne, Today.AddDays(1)),
            Event(4, BoardOne, Today.AddDays(1), cancelled: true),
            Event(5, BoardTwo, Today.AddDays(1))));

        await using var context = CreateContext(database);
        var count = await CreateRepository(context).CountUpcomingForBoardAsync(BoardOne, Today, Token);

        Assert.Equal(3, count.Total);
        Assert.Equal(1, count.Cancelled);
    }

    [Fact]
    public async Task CountUpcomingForBoard_OnlyPastEvents_IsZero()
    {
        var database = await SeedAsync(context => context.Events.AddRange(
            Event(1, BoardOne, Today.AddDays(-1)),
            Event(2, BoardOne, Today.AddDays(-30), cancelled: true)));

        await using var context = CreateContext(database);
        var count = await CreateRepository(context).CountUpcomingForBoardAsync(BoardOne, Today, Token);

        Assert.Equal(0, count.Total);
        Assert.Equal(0, count.Cancelled);
    }

    [Fact]
    public async Task CountActiveSeriesForBoard_OpenEndedOrEndingTodayOrLater_ExcludesEndedAndOtherBoards()
    {
        var database = await SeedAsync(context => context.EventSeries.AddRange(
            Series(1, BoardOne, null),
            Series(2, BoardOne, Today),
            Series(3, BoardOne, Today.AddDays(1)),
            Series(4, BoardOne, Today.AddDays(-1)),
            Series(5, BoardTwo, null)));

        await using var context = CreateContext(database);
        var count = await CreateRepository(context).CountActiveSeriesForBoardAsync(BoardOne, Today, Token);

        Assert.Equal(3, count);
    }

    private sealed class TestActiveGroupContext(int? activeGroupId) : IActiveGroupContext
    {
        public int? ActiveGroupId { get; } = activeGroupId;
    }
}
