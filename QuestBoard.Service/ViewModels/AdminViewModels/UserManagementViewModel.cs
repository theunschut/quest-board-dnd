using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

public class UserManagementViewModel
{
    public User User { get; set; } = new();
    public bool IsAdmin { get; set; }
    public bool IsDungeonMaster { get; set; }
    public bool IsPlayer { get; set; }
    public bool EmailConfirmed { get; set; }
}