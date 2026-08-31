namespace QuestBoard.Service.ViewModels.AgendaViewModels;

// Tells apart the three reasons an agenda page can render with no rows. AllBoardsFiltered
// is the only recoverable one -- the viewer can fix it with a single click -- so it is
// deliberately never folded into either of the other two.
public enum AgendaEmptyState
{
    None,
    NoBoards,
    NoUpcomingEvents,
    AllBoardsFiltered
}
