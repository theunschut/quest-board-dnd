using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.GroupPickerViewModels;

public class GroupPickerViewModel
{
    public IList<GroupWithMemberCount> Groups { get; set; } = [];
    public bool IsSuperAdmin { get; set; }
    public bool HasNoGroups { get; set; }
    public string? ReturnUrl { get; set; }
}
