using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

// One row of the cross-board agenda: an event, the viewer's own answer for it, and every
// other member's answer alongside it.
//
// There is deliberately no board-name or board-type property here. The domain Event model
// carries no group navigation, and the caller already has the board's name in memory from
// the membership read it has to perform anyway to build the set of board ids this row came
// from. Adding an include to thread a name through the shared Event model would widen a
// type every other consumer of Event depends on, just to save the caller one dictionary
// lookup it already has the data for.
public class AgendaRow
{
    public Event Event { get; init; } = new();

    public AvailabilityCellState MyCell { get; init; } = AvailabilityCellState.Empty;

    public IReadOnlyList<AgendaRosterEntry> Roster { get; init; } = [];
}
