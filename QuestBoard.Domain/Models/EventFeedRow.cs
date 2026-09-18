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
}
