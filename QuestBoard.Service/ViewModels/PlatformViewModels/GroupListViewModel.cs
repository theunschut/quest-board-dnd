using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupListViewModel
{
    public IList<GroupWithMemberCount> Groups { get; set; } = [];
}
