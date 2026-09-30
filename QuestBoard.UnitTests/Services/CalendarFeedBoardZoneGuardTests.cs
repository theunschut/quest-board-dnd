using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

// Pins the calendar feed's zoned contract with tests rather than a comment. A subscribed session
// must keep the same wall-clock hour on every subscriber's device, so every timed entry declares
// the board's zone by name and carries the stored digits exactly as they are, with no UTC
// designator; a generated time-zone block names that same zone; and the service hands the writer
// exactly the zone the board clock resolved, UTC included when the clock has fallen back. These
// facts are the guard that would catch a well-meaning change to CalendarFeedWriter or
// CalendarSubscriptionService that started converting a stored wall-clock value into another
// hour, or declared a different zone than the one the board runs on. Exact-byte assertion style,
// matching CalendarFeedWriterTests: plain string assertions, no snapshot framework.
public class CalendarFeedBoardZoneGuardTests
{
    private static readonly ICalendarFeedWriter Writer = new CalendarFeedWriter();
    private static readonly TimeZoneInfo AmsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
    private static readonly TimeZoneInfo AucklandZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    private static CalendarFeedEntry MakeFinalizedQuestEntry(DateOnly date, TimeOnly startTime, int sourceId = 1)
    {
        return new CalendarFeedEntry
        {
            Source = CalendarFeedSource.Quest,
            SourceId = sourceId,
            BoardName = "The Last Bastion",
            Title = "Session 12",
            Date = date,
            StartTime = startTime,
            Duration = TimeSpan.FromHours(4),
            CreatedAt = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    // Joins content lines the way the writer terminates them: every line, the last included,
    // ends in a carriage return and line feed.
    private static string Lines(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    [Fact]
    public void Write_EntriesEitherSideOfTheOctoberClockChange_DeclareTheZoneAndKeepTheirLocalDigits()
    {
        var before = MakeFinalizedQuestEntry(new DateOnly(2026, 10, 2), new TimeOnly(18, 0), sourceId: 1);
        var after = MakeFinalizedQuestEntry(new DateOnly(2026, 10, 30), new TimeOnly(18, 0), sourceId: 2);

        var body = Writer.Write([before, after], "D&D Quest Board", AmsterdamZone);

        // Both sessions read 18:00 on the wall even though the two dates sit either side of the
        // 25 October clock change: the offset differs, the digits do not.
        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20261002T180000\r\n");
        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20261030T180000\r\n");
        body.Should().Contain("DTEND;TZID=Europe/Amsterdam:20261030T220000\r\n");
        body.Should().NotContain("T180000Z");

        var expectedBlock = Lines(
            "BEGIN:VTIMEZONE",
            "TZID:Europe/Amsterdam",
            "BEGIN:DAYLIGHT",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0200",
            "END:DAYLIGHT",
            "BEGIN:STANDARD",
            "DTSTART:20261025T030000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0100",
            "END:STANDARD",
            "END:VTIMEZONE");
        body.Should().Contain(expectedBlock);
    }

    [Fact]
    public void Write_NonDefaultZone_DeclaresThatZoneAndNeverConvertsTheStoredDigits()
    {
        // The digits a Dungeon Master set are written as they are in any zone; the zone only says
        // where that hour lives.
        var entry = MakeFinalizedQuestEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "D&D Quest Board", AucklandZone);

        body.Should().Contain("DTSTART;TZID=Pacific/Auckland:20260920T190000\r\n");
        body.Should().Contain("DTEND;TZID=Pacific/Auckland:20260920T230000\r\n");
        body.Should().Contain("X-WR-TIMEZONE:Pacific/Auckland\r\n");
        body.Should().NotContain("T190000Z");
        body.Should().NotContain("Europe/Amsterdam");

        var expectedBlock = Lines(
            "BEGIN:VTIMEZONE",
            "TZID:Pacific/Auckland",
            "BEGIN:STANDARD",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+1200",
            "TZOFFSETTO:+1200",
            "END:STANDARD",
            "END:VTIMEZONE");
        body.Should().Contain(expectedBlock);
    }

    // Hand-rolled rather than substituted: CalendarSubscriptionService is internal, so a
    // dynamic proxy over ILogger<CalendarSubscriptionService> cannot be generated (Castle
    // DynamicProxy has no InternalsVisibleTo grant for an internal generic type argument).
    // Mirrors BoardClockTests' own CapturingLogger and EventsOverviewAggregationTests'
    // SilentLogger for the identical constraint. Nothing in this class asserts on logging.
    private sealed class SilentLogger : ILogger<CalendarSubscriptionService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private const int QuestId = 42;

    // One subscription for token "test-token", no event rows, and one finalized quest on a
    // one-shot board (2026-09-20 19:00), so the only entry the service builds is that quest.
    private static CalendarSubscriptionService BuildService(IBoardClock boardClock, ICalendarFeedWriter writer)
    {
        var subscription = new CalendarSubscription
        {
            Id = 1,
            UserId = 1,
            Name = "Test Subscription",
            Token = "test-token",
        };

        var quest = new QuestBoard.Domain.Models.QuestBoard.Quest
        {
            Id = QuestId,
            Title = "Board Zone Guard Session",
            GroupId = 1,
            DungeonMasterId = 1,
            FinalizedDate = new DateTime(2026, 9, 20, 19, 0, 0),
            IsFinalized = true,
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var subscriptionRepository = Substitute.For<ICalendarSubscriptionRepository>();
        subscriptionRepository.GetByTokenAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(subscription);

        var eventSignupRepository = Substitute.For<IEventSignupRepository>();
        eventSignupRepository
            .GetFeedRowsForUserAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<EventFeedRow>());

        var questRepository = Substitute.For<IQuestRepository>();
        questRepository
            .GetFeedQuestsForUserAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<QuestBoard.Domain.Models.QuestBoard.Quest> { quest });

        var groupService = Substitute.For<IGroupService>();
        groupService.GetGroupsForUserAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GroupWithMemberCount>
            {
                new() { Id = 1, Name = "Test Board", BoardType = BoardType.OneShot },
            });

        return new CalendarSubscriptionService(
            subscriptionRepository,
            eventSignupRepository,
            questRepository,
            groupService,
            writer,
            boardClock,
            TimeProvider.System,
            Options.Create(new CalendarFeedOptions()),
            new SilentLogger());
    }

    [Fact]
    public async Task GetFeedAsync_NonDefaultBoardClock_HandsTheWriterThatZoneAndAnUnshiftedEntry()
    {
        // A board clock resolving a non-default zone is handed to the service, exactly like the
        // standing production registration would carry one. The service must pass that very zone
        // to the writer, and the entry it builds must still carry the stored date and time
        // untouched. This fact fails if the service substitutes another zone, or if anyone routes
        // the finalized date through a conversion upstream of the writer.
        var clock = new FakeBoardClock { TimeZone = AucklandZone };

        IReadOnlyList<CalendarFeedEntry>? capturedEntries = null;
        TimeZoneInfo? capturedZone = null;
        var writer = Substitute.For<ICalendarFeedWriter>();
        writer.Write(
                Arg.Do<IReadOnlyList<CalendarFeedEntry>>(entries => capturedEntries = entries),
                Arg.Any<string>(),
                Arg.Do<TimeZoneInfo>(zone => capturedZone = zone))
            .Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        var service = BuildService(clock, writer);

        await service.GetFeedAsync("test-token", TestContext.Current.CancellationToken);

        capturedZone.Should().BeSameAs(clock.TimeZone);
        capturedZone!.Id.Should().Be("Pacific/Auckland");

        capturedEntries.Should().NotBeNull();
        var questEntry = capturedEntries!.Single(e => e.Source == CalendarFeedSource.Quest && e.SourceId == QuestId);
        questEntry.Date.Should().Be(new DateOnly(2026, 9, 20));
        questEntry.StartTime.Should().Be(new TimeOnly(19, 0));
    }

    [Fact]
    public async Task GetFeedAsync_DegradedBoardClock_HandsTheWriterUtc()
    {
        // A clock that could not resolve its configured zone falls back to UTC and says so; the
        // service still hands the writer whatever zone the clock exposes, never a fresh default.
        var clock = new FakeBoardClock { TimeZone = TimeZoneInfo.Utc, IsDegraded = true };

        IReadOnlyList<CalendarFeedEntry>? capturedEntries = null;
        TimeZoneInfo? capturedZone = null;
        var writer = Substitute.For<ICalendarFeedWriter>();
        writer.Write(
                Arg.Do<IReadOnlyList<CalendarFeedEntry>>(entries => capturedEntries = entries),
                Arg.Any<string>(),
                Arg.Do<TimeZoneInfo>(zone => capturedZone = zone))
            .Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        var service = BuildService(clock, writer);

        await service.GetFeedAsync("test-token", TestContext.Current.CancellationToken);

        capturedZone.Should().BeSameAs(TimeZoneInfo.Utc);

        capturedEntries.Should().NotBeNull();
        var questEntry = capturedEntries!.Single(e => e.Source == CalendarFeedSource.Quest && e.SourceId == QuestId);
        questEntry.Date.Should().Be(new DateOnly(2026, 9, 20));
        questEntry.StartTime.Should().Be(new TimeOnly(19, 0));
    }

    [Fact]
    public async Task GetFeedAsync_RealWriterUnderADegradedClock_DeclaresUtcThroughTheSamePath()
    {
        // No special branch for a degraded clock: the same zoned path runs, and the document
        // simply declares UTC with a zero-offset observance.
        var clock = new FakeBoardClock { TimeZone = TimeZoneInfo.Utc, IsDegraded = true };
        var service = BuildService(clock, new CalendarFeedWriter());

        var result = await service.GetFeedAsync("test-token", TestContext.Current.CancellationToken);

        var body = result.Body;
        body.Should().Contain("DTSTART;TZID=UTC:20260920T190000\r\n");
        body.Should().Contain("X-WR-TIMEZONE:UTC\r\n");
        body.Should().Contain("TZOFFSETTO:+0000\r\n");
        body.Should().NotContain("Europe/Amsterdam");
    }
}
