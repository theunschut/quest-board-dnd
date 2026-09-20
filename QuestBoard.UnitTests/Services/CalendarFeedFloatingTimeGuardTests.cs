using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// Pins the calendar feed's floating-local-time contract with a test rather than a comment, per
// this phase's own T-86-10 mitigation: CalendarFeedWriter.cs and CalendarSubscriptionService.cs
// must never appear in this phase's diff, and these facts are the guard that would catch it if
// either ever did. Exact-byte assertion style, matching CalendarFeedWriterTests: plain string
// assertions, no mocking of the writer itself, no snapshot framework.
public class CalendarFeedFloatingTimeGuardTests
{
    private static readonly ICalendarFeedWriter Writer = new CalendarFeedWriter();

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

        var body = Writer.Write([entry], "D&D Quest Board");

        // Exactly this digit sequence -- no trailing Z, no TZID parameter, no VTIMEZONE block
        // anywhere in the document.
        body.Should().Contain("DTSTART:20260920T190000\r\n");
        body.Should().NotContain("DTSTART:20260920T190000Z");
        body.Should().NotContain("TZID");
        body.Should().NotContain("VTIMEZONE");
    }

    [Fact]
    public void Write_FinalizedQuestEntry_UnaffectedByANonDefaultTimeZoneHeldInScope()
    {
        // Constructing (and even reading from) a TimeZoneInfo for a zone that is neither the
        // configured default nor UTC changes nothing about the emitted bytes -- the writer
        // takes DateOnly/TimeOnly, so there is no instant in scope for a zone to act on. This
        // is what makes the writer structurally immune, not merely untouched by this phase.
        var nonDefaultZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        _ = nonDefaultZone.BaseUtcOffset;

        var entry = MakeFinalizedQuestEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "D&D Quest Board");

        body.Should().Contain("DTSTART:20260920T190000\r\n");
        body.Should().NotContain("DTSTART:20260920T190000Z");
        body.Should().NotContain("TZID");
        body.Should().NotContain("VTIMEZONE");
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
        // A board clock resolving a non-default zone is constructed and held in scope here,
        // exactly like the standing production DI registration would carry one -- but
        // CalendarSubscriptionService takes a plain TimeProvider, not IBoardClock, so there is
        // no seam for this value to reach the service through. This fact is the assertion that
        // would fail if anyone ever routed FinalizedDate through a conversion upstream of the
        // writer.
        var nonDefaultBoardClock = new QuestBoard.UnitTests.Helpers.FakeBoardClock
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"),
        };
        _ = nonDefaultBoardClock.TimeZone;

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
        writer.Write(Arg.Do<IReadOnlyList<CalendarFeedEntry>>(entries => capturedEntries = entries), Arg.Any<string>())
            .Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        var service = new CalendarSubscriptionService(
            subscriptionRepository,
            eventSignupRepository,
            questRepository,
            groupService,
            writer,
            TimeProvider.System,
            Options.Create(new CalendarFeedOptions()),
            new SilentLogger());

        await service.GetFeedAsync("test-token", TestContext.Current.CancellationToken);

        capturedEntries.Should().NotBeNull();
        var questEntry = capturedEntries!.Single(e => e.Source == CalendarFeedSource.Quest && e.SourceId == quest.Id);

        // Unchanged when the registered board clock carries a non-default zone -- the service
        // never received it, so there was nothing for it to shift.
        questEntry.Date.Should().Be(new DateOnly(2026, 9, 20));
        questEntry.StartTime.Should().Be(new TimeOnly(19, 0));
    }
}
