using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.AgendaViewModels;

// One row of the cross-board agenda: an event, the board it belongs to, the viewer's own
// answer for it, and every other member's answer alongside it.
public class AgendaRowViewModel
{
    public int EventId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public int BoardId { get; set; }

    // BoardName, BoardType and IsActiveBoard are set by the controller after mapping,
    // because neither the board's name nor the viewer's current session board exists on
    // the source row -- the same way the availability overview's current-user id is set
    // by its controller rather than mapped.
    public string BoardName { get; set; } = string.Empty;

    public BoardType BoardType { get; set; }

    public bool IsActiveBoard { get; set; }

    public AvailabilityCellState MyCell { get; set; }

    public IList<AgendaRosterEntryViewModel> Roster { get; set; } = [];
}
