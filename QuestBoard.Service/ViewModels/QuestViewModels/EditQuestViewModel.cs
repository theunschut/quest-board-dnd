using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.QuestViewModels;

public class EditQuestViewModel
{
    public int Id { get; set; }
    public QuestViewModel Quest { get; set; } = new();
    public IList<User> DungeonMasters { get; set; } = [];
    public bool CanEditProposedDates { get; set; }
    public bool HasExistingSignups { get; set; }
}