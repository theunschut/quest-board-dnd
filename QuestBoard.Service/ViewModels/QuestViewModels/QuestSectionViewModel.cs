using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.QuestViewModels;

public class QuestSectionViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string CollapseTargetId { get; set; } = string.Empty;
    public bool IsExpanded { get; set; } = false;
    public IEnumerable<Quest> Quests { get; set; } = [];
    public string EmptyMessage { get; set; } = string.Empty;
}