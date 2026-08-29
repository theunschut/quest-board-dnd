using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

// One row of the availability grid: an event plus its three headline counts and the
// per-member cell states for that event.
public class EventAvailabilityRow
{
    public Event Event { get; init; } = new();

    // Total Yes answers, including unconfirmed defaults -- the whole point of tracking
    // this separately from ConfirmedYesCount.
    public int YesCount { get; init; }

    // The subset of YesCount whose member actually answered, rather than having a Yes
    // stamped on their row automatically.
    public int ConfirmedYesCount { get; init; }

    // Kept entirely separate from YesCount -- a Maybe is never folded into an available
    // headcount. There is deliberately no NoCount: a No answer is visible in its own cell
    // and is not counted anywhere on the page.
    public int MaybeCount { get; init; }

    // Positionally aligned with EventAvailabilityOverview.Members -- Cells.Count always
    // equals Members.Count, and Cells[i] is this row's state for Members[i]. That
    // invariant is what keeps column order stable across every row.
    public IReadOnlyList<AvailabilityCellState> Cells { get; init; } = [];
}
