using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

// The domain Event model has no signup navigation of its own, so this is the read shape for a
// query that fetches an event together with the caller's own answer on it. It is a read-only
// pairing produced by a single query, not a navigation property -- mirroring EventWithSignups'
// own shape.
public class EventFeedRow
{
    public Event Event { get; init; } = new();

    public VoteType Availability { get; init; }

    // The last time this reader's own answer row was written: when a person last set the
    // answer, or when the row was created if no person ever has. It is a real instant that only
    // moves forward, even if the row is deleted and made again, which is what lets the feed use
    // it as the reader's own revision stamp without touching the event's shared revision.
    public DateTime AnswerWrittenAt { get; init; }
}
