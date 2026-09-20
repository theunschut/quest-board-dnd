using QuestBoard.Domain.Interfaces;

namespace QuestBoard.UnitTests.Helpers;

// Hand-rolled rather than a testing-time-provider package, so the fixed clock costs no new
// test dependency -- mirrors EventsOverviewAggregationTests' own FixedTimeProvider fixture.
// Shared across suites that need a board clock double without exercising BoardClock's own
// zone-resolution logic.
public sealed class FakeBoardClock : IBoardClock
{
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

    public bool IsDegraded { get; set; }

    public DateOnly Today { get; set; }

    public DateTime Now { get; set; }
}
