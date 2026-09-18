using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.AccountViewModels;

public class ProfileViewModel
{
    public User? User { get; set; }

    // Defaults to an empty list so a layout that renders this section never has to null-check it.
    public IReadOnlyList<CalendarSubscriptionViewModel> CalendarSubscriptions { get; set; } = [];
}