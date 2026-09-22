using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// Pins BoardClock's two contracts: an unresolvable configured zone degrades to UTC rather than
// crashing the application, and Now/Today are computed from the resolved zone rather than
// ambient DateTime.UtcNow -- the midnight-to-02:00 window this whole phase exists to fix.
public class BoardClockTests
{
    // Hand-rolled rather than a testing-time-provider package, so the fixed clock costs no new
    // test dependency -- mirrors EventsOverviewAggregationTests' own FixedTimeProvider fixture.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Hand-rolled rather than substituted: BoardClock is internal, so a dynamic proxy over
    // ILogger<BoardClock> cannot be generated (the same constraint EventsOverviewAggregationTests
    // documents for ILogger<EventService>). Records whether a Warning-level call was made, which
    // is all these tests need to assert.
    private sealed class CapturingLogger : ILogger<BoardClock>
    {
        public bool ReceivedWarning { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                ReceivedWarning = true;
            }
        }
    }

    private static BoardClock CreateClock(string zoneId, TimeProvider timeProvider, CapturingLogger logger)
    {
        var options = Options.Create(new TimeZoneOptions { BoardTimeZoneId = zoneId });
        return new BoardClock(timeProvider, options, logger);
    }

    [Fact]
    public void BoardClockFallback_UnresolvableZoneId_DegradesToUtcWithoutThrowing()
    {
        var logger = new CapturingLogger();

        var act = () => CreateClock("Definitely/NotAZone", TimeProvider.System, logger);

        var clock = act.Should().NotThrow().Which;
        clock.IsDegraded.Should().BeTrue();
        clock.TimeZone.Should().Be(TimeZoneInfo.Utc);
        logger.ReceivedWarning.Should().BeTrue(
            because: "a fallback to UTC must be visible to an operator via a logged warning");
    }

    [Fact]
    public void BoardClockFallback_ResolvableZoneId_IsNotDegraded()
    {
        var logger = new CapturingLogger();

        // TimeZoneInfo.Local.Id is host-independent -- it resolves on any machine this test
        // runs on, without depending on a specific IANA zone being installed.
        var clock = CreateClock(TimeZoneInfo.Local.Id, TimeProvider.System, logger);

        clock.IsDegraded.Should().BeFalse();
        logger.ReceivedWarning.Should().BeFalse();
    }

    [Fact]
    public void BoardClock_NowAndToday_CrossTheDateBoundaryUnderAPlusTwoZone()
    {
        var logger = new CapturingLogger();
        var fixedInstant = new DateTimeOffset(2026, 9, 20, 23, 30, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedInstant);

        // "Etc/GMT-2" is a fixed +02:00 offset with no daylight-saving transitions, and (despite
        // its inverted POSIX sign) is one of the IANA zones every .NET runtime -- Windows'
        // ICU-backed lookup and Linux's tzdata alike -- resolves identically, so this assertion
        // does not depend on which IANA zone the host happens to have installed for a real place.
        var clock = CreateClock("Etc/GMT-2", timeProvider, logger);

        clock.IsDegraded.Should().BeFalse();
        // The whole point of this phase: a 23:30 UTC instant is already the next calendar day
        // in a +02:00 zone -- the window an ambient UTC read gets wrong.
        clock.Now.Should().Be(new DateTime(2026, 9, 21, 1, 30, 0));
        clock.Today.Should().Be(new DateOnly(2026, 9, 21));
    }
}
