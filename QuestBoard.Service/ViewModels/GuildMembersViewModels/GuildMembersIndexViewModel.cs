using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.GuildMembersViewModels;

public class GuildMembersIndexViewModel
{
    public IEnumerable<User> DungeonMasters { get; set; } = [];
    public IEnumerable<User> Players { get; set; } = [];
}