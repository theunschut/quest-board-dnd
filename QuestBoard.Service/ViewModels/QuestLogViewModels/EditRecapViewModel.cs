using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.QuestLogViewModels;

public class EditRecapViewModel
{
    public int Id { get; set; }
    public string? Recap { get; set; }
    public Quest Quest { get; set; } = new();
}
