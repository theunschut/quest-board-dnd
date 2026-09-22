using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

// Pins every clock-dependent branch in EventSeriesService against a fake board clock whose
// Today deliberately differs from the host's own DateTime.Today, so a regression that brings
// back an ambient clock read fails here instead of passing by coincidence.
public class EventSeriesServiceTests
{
    private static EventSeriesService CreateService(
        IEventSeriesRepository? repository = null,
        IEventRepository? eventRepository = null,
        IUserRepository? userRepository = null,
        IBoardTypeResolver? boardTypeResolver = null,
        IActiveGroupContext? activeGroupContext = null,
        IOptions<EventSeriesOptions>? options = null,
        IBoardClock? boardClock = null)
    {
        return new EventSeriesService(
            repository ?? Substitute.For<IEventSeriesRepository>(),
            eventRepository ?? Substitute.For<IEventRepository>(),
            userRepository ?? Substitute.For<IUserRepository>(),
            boardTypeResolver ?? Substitute.For<IBoardTypeResolver>(),
            activeGroupContext ?? Substitute.For<IActiveGroupContext>(),
            options ?? Options.Create(new EventSeriesOptions()),
            boardClock ?? new FakeBoardClock { Today = new DateOnly(2026, 9, 21) });
    }

    // -------------------------------------------------------------------
    // PreviewAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_AnchorFullyInPast_MatchesBoardClockTodayBoundary()
    {
        // Arrange: the board clock runs ahead of the host's own today, with the single firing slot
        // placed between the two -- already past from the board's point of view, still future from
        // the host's. An ambient read therefore reports the anchor as not-yet-past and flips this
        // assertion. Both dates are offset from the same host read, so the gap stays fixed and the
        // test keeps discriminating however far the real calendar moves; a slot on a hard-coded
        // date would fall into the host's past too and quietly stop testing anything.
        var hostToday = DateOnly.FromDateTime(DateTime.Today);
        var boardClock = new FakeBoardClock { Today = hostToday.AddDays(2) };
        var service = CreateService(boardClock: boardClock);
        var anchorDate = hostToday.AddDays(1);

        // Act
        var (dates, anchorFullyInPast) = await service.PreviewAsync(
            anchorDate, intervalWeeks: 4, cycleMask: "1", endDate: anchorDate, TestContext.Current.CancellationToken);

        // Assert
        anchorFullyInPast.Should().BeTrue();
        dates.Should().BeEmpty();
    }

    // -------------------------------------------------------------------
    // GetActiveSeriesForActiveGroupAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetActiveSeriesForActiveGroupAsync_ForwardsBoardClockToday_NotHostClock()
    {
        // Arrange: the repository owns the actual end-date filtering (a series ending on the
        // board's today is included, one ending the day before is excluded) -- that behaviour is
        // exercised where the repository lives. This test's job is to pin that the service asks
        // the repository about the board clock's today rather than the host's.
        var boardClock = new FakeBoardClock { Today = new DateOnly(2026, 9, 21) };
        var repository = Substitute.For<IEventSeriesRepository>();
        var expected = new List<EventSeries> { new() { Id = 1, EndDate = new DateOnly(2026, 9, 21) } };
        repository.GetActiveSeriesAsync(new DateOnly(2026, 9, 21), Arg.Any<CancellationToken>()).Returns(expected);
        var service = CreateService(repository: repository, boardClock: boardClock);

        // Act
        var result = await service.GetActiveSeriesForActiveGroupAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(expected);
        await repository.Received(1).GetActiveSeriesAsync(new DateOnly(2026, 9, 21), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------
    // GetSeriesBelowRunwayAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetSeriesBelowRunwayAsync_CountsFromBoardClockToday_NotHostClock()
    {
        // Arrange
        var boardClock = new FakeBoardClock { Today = new DateOnly(2026, 9, 21) };
        var repository = Substitute.For<IEventSeriesRepository>();
        var expected = new List<SeriesRunwayStatus> { new() { SeriesId = 1, Title = "Weekly Delve", UpcomingCount = 3 } };
        repository.GetSeriesBelowRunwayAsync(new DateOnly(2026, 9, 21), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var service = CreateService(repository: repository, boardClock: boardClock);

        // Act
        var result = await service.GetSeriesBelowRunwayAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(expected);
        await repository.Received(1).GetSeriesBelowRunwayAsync(new DateOnly(2026, 9, 21), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------
    // TopUpAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task TopUpAsync_GeneratesFromBoardClockToday_NotHostClock()
    {
        // Arrange: a run "at 00:30 board time" is represented here purely as a DateOnly the
        // service must read from the board clock rather than from the host container's own
        // ambient today.
        var boardClock = new FakeBoardClock { Today = new DateOnly(2026, 9, 21) };
        var series = new EventSeries
        {
            Id = 1,
            AnchorDate = new DateOnly(2026, 9, 21),
            IntervalWeeks = 1,
            CycleMask = "1",
            EndDate = null,
            GroupId = 7
        };
        var repository = Substitute.For<IEventSeriesRepository>();
        repository.GetSeriesAsync(1, Arg.Any<CancellationToken>()).Returns(series);
        repository.GetSlotIndexesForSeriesAsync(1, Arg.Any<CancellationToken>()).Returns(new List<int>());
        repository.CountLiveFutureOccurrencesAsync(1, new DateOnly(2026, 9, 21), Arg.Any<CancellationToken>()).Returns(0);
        var eventRepository = Substitute.For<IEventRepository>();
        var boardTypeResolver = Substitute.For<IBoardTypeResolver>();
        boardTypeResolver.GetBoardTypeAsync(Arg.Any<CancellationToken>()).Returns(BoardType.OneShot);
        var activeGroupContext = Substitute.For<IActiveGroupContext>();
        activeGroupContext.ActiveGroupId.Returns(7);
        var options = Options.Create(new EventSeriesOptions { RunwaySize = 1 });
        var service = CreateService(
            repository: repository,
            eventRepository: eventRepository,
            boardTypeResolver: boardTypeResolver,
            activeGroupContext: activeGroupContext,
            options: options,
            boardClock: boardClock);

        // Act
        var created = await service.TopUpAsync(1, TestContext.Current.CancellationToken);

        // Assert
        created.Should().Be(1);
        await eventRepository.Received(1).AddAsync(Arg.Is<Event>(e => e.Date == new DateOnly(2026, 9, 21)), Arg.Any<CancellationToken>());
        await repository.Received(1).CountLiveFutureOccurrencesAsync(1, new DateOnly(2026, 9, 21), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------
    // ApplyTemplateToFutureAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task ApplyTemplateToFutureAsync_EligibilityUsesBoardClockToday_NotHostClock()
    {
        // Arrange: the board clock's today sits before the host's real calendar date, so an
        // occurrence dated in between is only eligible when the service reads the board clock.
        var boardClock = new FakeBoardClock { Today = new DateOnly(2026, 9, 15) };
        var series = new EventSeries
        {
            Id = 1,
            AnchorDate = new DateOnly(2026, 9, 11),
            IntervalWeeks = 1,
            Title = "Old Title",
            Description = "Old Description",
            StartTime = new TimeOnly(19, 0)
        };
        var repository = Substitute.For<IEventSeriesRepository>();
        repository.GetSeriesAsync(1, Arg.Any<CancellationToken>()).Returns(series);
        repository.SetTemplateAsync(1, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeOnly?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var eligibleOccurrence = new Event
        {
            Id = 2,
            SeriesSlotIndex = 1,
            Date = new DateOnly(2026, 9, 18),
            Title = series.Title,
            Description = series.Description,
            StartTime = series.StartTime
        };
        var eventRepository = Substitute.For<IEventRepository>();
        eventRepository.GetOccurrencesForSeriesAsync(1, Arg.Any<CancellationToken>()).Returns(new List<Event> { eligibleOccurrence });
        eventRepository.ApplyTemplateToOccurrencesAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeOnly?>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var service = CreateService(repository: repository, eventRepository: eventRepository, boardClock: boardClock);

        // Act
        await service.ApplyTemplateToFutureAsync(1, editedEventId: 99, "New Title", "New Description", new TimeOnly(20, 0), TestContext.Current.CancellationToken);

        // Assert
        await eventRepository.Received(1).ApplyTemplateToOccurrencesAsync(
            Arg.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 2),
            "New Title", "New Description", new TimeOnly(20, 0), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------
    // CreateWithFirstPassAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task CreateWithFirstPassAsync_FirstPassWindowUsesBoardClockToday_NotHostClock()
    {
        // Arrange: two firing slots, one before the board clock's today and one on/after it.
        // Only the one on/after should be materialized.
        var boardClock = new FakeBoardClock { Today = new DateOnly(2026, 9, 15) };
        var activeGroupContext = Substitute.For<IActiveGroupContext>();
        activeGroupContext.ActiveGroupId.Returns(7);
        var boardTypeResolver = Substitute.For<IBoardTypeResolver>();
        boardTypeResolver.GetBoardTypeAsync(Arg.Any<CancellationToken>()).Returns(BoardType.OneShot);
        var repository = Substitute.For<IEventSeriesRepository>();
        IReadOnlyList<Event>? capturedOccurrences = null;
        repository.CreateWithOccurrencesAsync(
            Arg.Any<EventSeries>(),
            Arg.Do<IReadOnlyList<Event>>(o => capturedOccurrences = o),
            Arg.Any<IReadOnlyCollection<int>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var options = Options.Create(new EventSeriesOptions { RunwaySize = 20, PreviewCount = 10 });
        var service = CreateService(
            repository: repository,
            boardTypeResolver: boardTypeResolver,
            activeGroupContext: activeGroupContext,
            options: options,
            boardClock: boardClock);

        var series = new EventSeries
        {
            AnchorDate = new DateOnly(2026, 9, 11),
            IntervalWeeks = 1,
            CycleMask = "1",
            EndDate = new DateOnly(2026, 9, 22)
        };

        // Act
        await service.CreateWithFirstPassAsync(series, TestContext.Current.CancellationToken);

        // Assert
        capturedOccurrences.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 9, 18));
    }
}
