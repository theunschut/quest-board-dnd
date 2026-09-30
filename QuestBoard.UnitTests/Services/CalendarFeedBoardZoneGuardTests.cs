using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// Pins the calendar feed's zoned wall-clock contract with a test rather than a comment. A
// subscribed session must keep the same wall-clock hour on every subscriber's device, so the feed
// declares the board's zone on every timed entry and writes the stored digits exactly as they are,
// with no trailing Z. These facts are the guard that would catch a well-meaning change to
// CalendarFeedWriter or CalendarSubscriptionService that started converting a stored wall-clock
// value into another hour. Exact-byte assertion style, matching CalendarFeedWriterTests: plain
// string assertions, no mocking of the writer itself, no snapshot framework.
public class CalendarFeedBoardZoneGuardTests
{
    private static readonly ICalendarFeedWriter Writer = new CalendarFeedWriter();
    private static readonly TimeZoneInfo AmsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

    private static CalendarFeedEntry MakeFinalizedQuestEntry(DateOnly date, TimeOnly startTime)
    {
        return new CalendarFeedEntry
        {
            Source = CalendarFeedSource.Quest,
            SourceId = 1,
            BoardName = "The Last Bastion",
            Title = "Session 12",
            Date = date,
            StartTime = startTime,
            Duration = TimeSpan.FromHours(4),
            CreatedAt = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    [Fact]
    public void Write_FinalizedQuestEntry_EmitsExactFloatingDtstartWithNoZoneDesignator()
    {
        var entry = MakeFinalizedQuestEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "D&D Quest Board", AmsterdamZone);

        // Exactly this digit sequence, with the board's zone declared beside it -- no trailing Z,
        // and the digits are the stored ones, not a converted hour.
        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20260920T190000\r\n");
        body.Should().NotContain("20260920T190000Z");
        body.Should().Contain("BEGIN:VTIMEZONE\r\nTZID:Europe/Amsterdam\r\n");
    }

    [Fact]
    public void Write_FinalizedQuestEntry_UnaffectedByANonDefaultTimeZoneHeldInScope()
    {
        // Handing the writer a zone far from the stored hour changes which zone is declared but
        // never the digits: the writer takes DateOnly/TimeOnly and only says which zone they
        // belong to, so a 19:00 game night stays 190000 whatever the board's zone is.
        var nonDefaultZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

        var entry = MakeFinalizedQuestEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "D&D Quest Board", nonDefaultZone);

        body.Should().Contain("DTSTART;TZID=Pacific/Auckland:20260920T190000\r\n");
        body.Should().NotContain("20260920T190000Z");
        body.Should().Contain("TZID:Pacific/Auckland\r\n");
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

    [Fact]
    public async Task GetFeedAsync_FinalizedQuest_ProducesAnUnshiftedEntry_EvenWithANonDefaultBoardClockInScope()
    {
        // A board clock resolving a non-default zone is handed to the service, exactly like the
        // standing production DI registration would carry one. The service passes the zone on to
        // the writer, but the entry it builds must still carry the stored date and time
        // untouched. This fact is the assertion that would fail if anyone ever routed
        // FinalizedDate through a conversion upstream of the writer.
        var nonDefaultBoardClock = new QuestBoard.UnitTests.Helpers.FakeBoardClock
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"),
        };

        var finalizedDate = new DateTime(2026, 9, 20, 19, 0, 0);

        var subscription = new CalendarSubscription
        {
            Id = 1,
            UserId = 1,
            Name = "Test Subscription",
            Token = "test-token",
        };

        var quest = new QuestBoard.Domain.Models.QuestBoard.Quest
        {
            Id = 42,
            Title = "Floating Time Guard Session",
            GroupId = 1,
            DungeonMasterId = 1,
            FinalizedDate = finalizedDate,
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

        IReadOnlyList<CalendarFeedEntry>? capturedEntries = null;
        var writer = Substitute.For<ICalendarFeedWriter>();
        writer.Write(Arg.Do<IReadOnlyList<CalendarFeedEntry>>(entries => capturedEntries = entries), Arg.Any<string>(), Arg.Any<TimeZoneInfo>())
            .Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        var service = new CalendarSubscriptionService(
            subscriptionRepository,
            eventSignupRepository,
            questRepository,
            groupService,
            writer,
            nonDefaultBoardClock,
            TimeProvider.System,
            Options.Create(new CalendarFeedOptions()),
            new SilentLogger());

        await service.GetFeedAsync("test-token", TestContext.Current.CancellationToken);

        capturedEntries.Should().NotBeNull();
        var questEntry = capturedEntries!.Single(e => e.Source == CalendarFeedSource.Quest && e.SourceId == quest.Id);

        // Unchanged when the board clock carries a non-default zone -- the service only forwards
        // the zone, so there was nothing for it to shift.
        questEntry.Date.Should().Be(new DateOnly(2026, 9, 20));
        questEntry.StartTime.Should().Be(new TimeOnly(19, 0));
    }
}
