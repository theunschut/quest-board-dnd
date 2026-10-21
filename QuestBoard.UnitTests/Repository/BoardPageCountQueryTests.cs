using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Repository;

public class BoardPageCountQueryTests
{
    private const int BoardOne = 1;
    private const int BoardTwo = 2;

    private static readonly DateOnly Today = new(2031, 6, 15);

    // Mid-morning, so the evening before is still inside the rolling day the quest list keeps.
    private static readonly DateTime Now = new(2031, 6, 15, 9, 0, 0);

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

    private static (QuestRepository Repository, IMapper Mapper) CreateQuestRepository(QuestBoardContext context)
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        var mapper = configuration.CreateMapper();
        return (new QuestRepository(context, mapper, new FakeBoardClock { Today = Today, Now = Now }), mapper);
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

    private static QuestEntity Quest(int id, int groupId, bool closed = false, DateTime? finalizedDate = null) => new()
    {
        Id = id,
        GroupId = groupId,
        DungeonMasterId = 1,
        Title = $"Quest {id}",
        Description = "d",
        IsClosed = closed,
        IsFinalized = finalizedDate.HasValue,
        FinalizedDate = finalizedDate
    };

    private static List<QuestEntity> LiveBoundaryQuests() =>
    [
        // Open with nothing finalized: counts.
        Quest(1, BoardOne),
        // Open with only past proposed dates: counts, because nobody has finalized or deleted it.
        Quest(2, BoardOne),
        // Finalized the evening before today: the quest list still shows it until the same time
        // today, so it counts even though a calendar-date rule would call it played.
        Quest(3, BoardOne, finalizedDate: new DateTime(2031, 6, 14, 23, 0, 0)),
        // Finalized at midnight today: not yet a full day past, so live.
        Quest(4, BoardOne, finalizedDate: new DateTime(2031, 6, 15, 0, 0, 0)),
        Quest(5, BoardOne, finalizedDate: new DateTime(2031, 6, 20, 19, 0, 0)),
        // Finalized the morning before today, earlier than the board's time of day: a full day
        // past, so the list has dropped it and it is not live.
        Quest(10, BoardOne, finalizedDate: new DateTime(2031, 6, 14, 8, 0, 0)),
        // Closed quests are never live, finalized or not.
        Quest(6, BoardOne, closed: true),
        Quest(7, BoardOne, closed: true, finalizedDate: new DateTime(2031, 7, 1, 19, 0, 0)),
        // Another board's quest is never counted.
        Quest(8, BoardTwo),
        Quest(9, BoardTwo, finalizedDate: new DateTime(2031, 6, 20, 19, 0, 0))
    ];

    private static void SeedLiveBoundaryQuests(QuestBoardContext context)
    {
        // The quest list loads each quest's dungeon master, so the rows it reads need one to exist.
        context.Users.Add(new UserEntity { Id = 1, Name = "Dungeon Master", UserName = "dm" });
        var quests = LiveBoundaryQuests();
        quests.Single(q => q.Id == 2).ProposedDates.Add(new ProposedDateEntity { Id = 1, Date = Today.AddDays(-10).ToDateTime(TimeOnly.MinValue) });
        context.Quests.AddRange(quests);
    }

    [Fact]
    public async Task CountLiveForBoard_BoundaryCases_CountsOnlyQuestsTheBoardStillListsForThatBoard()
    {
        var database = await SeedAsync(SeedLiveBoundaryQuests);

        await using var context = CreateContext(database);
        var (repository, _) = CreateQuestRepository(context);
        var count = await repository.CountLiveForBoardAsync(BoardOne, Now, Token);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task CountLiveForBoard_OtherBoardIsCountedOnItsOwnId()
    {
        var database = await SeedAsync(SeedLiveBoundaryQuests);

        await using var context = CreateContext(database);
        var (repository, _) = CreateQuestRepository(context);
        var count = await repository.CountLiveForBoardAsync(BoardTwo, Now, Token);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CountLiveForBoard_NoQuests_IsZero()
    {
        var database = await SeedAsync(_ => { });

        await using var context = CreateContext(database);
        var (repository, _) = CreateQuestRepository(context);

        Assert.Equal(0, await repository.CountLiveForBoardAsync(BoardOne, Now, Token));
    }

    [Fact]
    public async Task CountLiveForBoard_MatchesWhatTheQuestListShows_AtTheRollingDayBoundary()
    {
        var database = await SeedAsync(SeedLiveBoundaryQuests);

        // Reading as the board itself, so the list applies the same cutoff the count is judged against.
        await using var context = CreateContext(database, BoardOne);
        var (repository, _) = CreateQuestRepository(context);

        var listed = await repository.GetQuestsWithSignupsAsync(Token);

        Assert.Equal(listed.Count, await repository.CountLiveForBoardAsync(BoardOne, Now, Token));
        Assert.Contains(listed, q => q.Id == 3);
        Assert.DoesNotContain(listed, q => q.Id == 10);
    }

    private sealed class TestActiveGroupContext(int? activeGroupId) : IActiveGroupContext
    {
        public int? ActiveGroupId { get; } = activeGroupId;
    }
}
