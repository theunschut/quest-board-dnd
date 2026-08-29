namespace QuestBoard.Service.ViewModels.EventViewModels;

// One column of the availability grid: a member who holds at least one signup row across
// the events being shown.
public class OverviewMemberViewModel
{
    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;
}
