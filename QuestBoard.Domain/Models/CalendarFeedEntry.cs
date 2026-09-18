using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class CalendarFeedEntry
{
    public CalendarFeedSource Source { get; set; }

    public int SourceId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    // A null start time means a true all-day entry, rendered through the writer's all-day
    // branch; a non-null value means a timed entry, rendered through its timed branch.
    public TimeOnly? StartTime { get; set; }

    // How long the timed block runs. Defaults to one hour so every existing event call site and
    // every existing writer unit test stays byte-identical without being touched -- only a
    // source whose duration differs from an hour needs to set this explicitly.
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);

    public VoteType Availability { get; set; }

    // Carries the event's own creation time so the emitted timestamp property is stable
    // between fetches rather than moving on every poll.
    public DateTime CreatedAt { get; set; }
}
