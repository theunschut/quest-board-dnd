namespace QuestBoard.Domain.Models;

// The domain Event model deliberately has no Signups navigation, so this is the read
// shape for a repository call that fetches an event together with its signup rows. It is
// a read-only pairing produced by a single query, not a navigation property on Event.
public class EventWithSignups
{
    public Event Event { get; init; } = new();

    public IReadOnlyList<EventSignup> Signups { get; init; } = [];
}
