namespace QuestBoard.Service.ViewModels.EventViewModels;

// The whole availability grid: the shared member axis (columns), one row per event, and
// the paging state the Show More control and the alignment check both need.
public class EventOverviewViewModel
{
    public IList<OverviewMemberViewModel> Members { get; set; } = [];

    public IList<EventOverviewRowViewModel> Rows { get; set; } = [];

    public bool HasMore { get; set; }

    public int Take { get; set; }

    // The value the Show More control links to, precomputed server-side so the view never
    // does arithmetic on a client-supplied number.
    public int NextTake { get; set; }

    // Lets the view highlight the viewer's own column without disturbing the alphabetical
    // column order.
    public int CurrentUserId { get; set; }
}
