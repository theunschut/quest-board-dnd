namespace QuestBoard.Service.ViewModels.AgendaViewModels;

// The whole cross-board agenda: one row per upcoming event across every board the viewer
// belongs to (narrowed by the filter), the filter checklist, and the paging values the
// growth flag below is computed from.
public class AgendaViewModel
{
    public IList<AgendaRowViewModel> Rows { get; set; } = [];

    public IList<AgendaBoardOptionViewModel> AvailableBoards { get; set; } = [];

    public int SelectedCount { get; set; }

    public int TotalCount { get; set; }

    // The comma-separated effective selection, carried into the paging link so growing
    // the window never silently resets the filter.
    public string SelectedBoardIds { get; set; } = string.Empty;

    public bool HasMore { get; set; }

    public int Take { get; set; }

    // The value the Show More control links to, precomputed server-side so the view never
    // does arithmetic on a client-supplied number.
    public int NextTake { get; set; }

    public int CurrentUserId { get; set; }

    public string? ActiveBoardName { get; set; }

    public AgendaEmptyState EmptyState { get; set; }

    // Further events existing is not by itself enough to offer growth, because the window
    // can already sit on its ceiling -- in that case NextTake equals Take and a Show More
    // control would only link back to the page the reader is already on.
    public bool CanShowMore => HasMore && NextTake > Take;
}
