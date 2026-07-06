using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlayersViewModels;

public class PlayersIndexViewModel
{
    public IEnumerable<User> DungeonMasters { get; set; } = [];
    public IEnumerable<User> Players { get; set; } = [];
}
