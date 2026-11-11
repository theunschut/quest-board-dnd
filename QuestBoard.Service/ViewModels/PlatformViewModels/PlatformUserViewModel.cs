using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class PlatformUserViewModel
{
    public required User User { get; set; }
    public bool IsDisabled { get; set; }
    public IReadOnlyList<string> BoardNames { get; set; } = [];
}
