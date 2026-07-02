using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupMembersViewModel
{
    public Group Group { get; set; } = null!;
    public IList<UserGroup> Members { get; set; } = [];
    public AddMemberViewModel AddMember { get; set; } = new();
    public IList<User> AvailableUsers { get; set; } = [];
}
