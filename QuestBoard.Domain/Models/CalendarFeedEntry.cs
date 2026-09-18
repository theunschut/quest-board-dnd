using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class CalendarFeedEntry
{
    public CalendarFeedSource Source { get; set; }

    public int SourceId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    // A null start time means the entry is a true all-day event -- unhandled by this phase's
    // writer, which covers the timed branch only.
    public TimeOnly? StartTime { get; set; }

    public VoteType Availability { get; set; }

    // Carries the event's own creation time so the emitted timestamp property is stable
    // between fetches rather than moving on every poll.
    public DateTime CreatedAt { get; set; }
}
