namespace QuestBoard.Domain.Interfaces;

// The one seam every consumer reads the board's own wall-clock zone through, so a cron
// registration and an ambient "what day is it" read can never resolve two different zones.
public interface IBoardClock
{
    TimeZoneInfo TimeZone { get; }

    // True when the configured zone id could not be resolved and the clock fell back to UTC --
    // the application still boots and runs, just with the board's own zone no longer applied.
    bool IsDegraded { get; }

    DateOnly Today { get; }

    DateTime Now { get; }
}
