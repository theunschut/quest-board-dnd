using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.QuestLogViewModels;

public class QuestLogIndexViewModel
{
    public IEnumerable<Quest> CompletedQuests { get; set; } = [];
}
