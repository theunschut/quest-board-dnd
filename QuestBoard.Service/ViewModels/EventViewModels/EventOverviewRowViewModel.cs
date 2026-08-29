using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.EventViewModels;

// One row of the availability grid: an event plus its three headline counts and the
// per-member cell states for that event.
public class EventOverviewRowViewModel
{
    public int EventId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    // A null start time means the event runs all day.
    public TimeOnly? StartTime { get; set; }

    // Total Yes answers, including unconfirmed defaults -- the whole point of tracking
    // this separately from ConfirmedYesCount.
    public int YesCount { get; set; }

    // The subset of YesCount whose member actually answered, rather than having a Yes
    // stamped on their row automatically.
    public int ConfirmedYesCount { get; set; }

    // Kept entirely separate from YesCount -- a Maybe is never folded into an available
    // headcount. There is deliberately no count for No: a No answer is visible in its own
    // cell and is not counted anywhere on the page.
    public int MaybeCount { get; set; }

    // Positionally aligned with EventOverviewViewModel.Members -- Cells.Count always
    // equals Members.Count, and Cells[i] is this row's state for Members[i]. That
    // invariant is what keeps column order stable across every row.
    public IList<AvailabilityCellState> Cells { get; set; } = [];
}
