using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

// One member's answer for a single agenda row -- every signup on the event becomes one of
// these, so the roster is complete rather than a summary count.
public class AgendaRosterEntry
{
    public int UserId { get; init; }

    public string Name { get; init; } = string.Empty;

    public AvailabilityCellState Cell { get; init; } = AvailabilityCellState.Empty;

    // True for exactly the entry belonging to the viewer requesting the agenda, so the
    // caller can highlight their own row within the roster without a second comparison.
    public bool IsViewer { get; init; }
}
