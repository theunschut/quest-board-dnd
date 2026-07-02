using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.QuestLogViewModels;

public class QuestLogDetailsViewModel
{
    public Quest Quest { get; set; } = new();
}
