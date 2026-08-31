namespace QuestBoard.Domain.Models;

// The whole availability grid: the shared member axis, one row per event, and whether
// more events exist beyond what was fetched.
public class EventAvailabilityOverview
{
    public IReadOnlyList<AvailabilityMember> Members { get; init; } = [];

    public IReadOnlyList<EventAvailabilityRow> Rows { get; init; } = [];

    public bool HasMore { get; init; }
}
