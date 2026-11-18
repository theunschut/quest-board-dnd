using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

public class UserManagementViewModel
{
    public User User { get; set; } = new();
    public GroupRole? Role { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsDungeonMaster { get; set; }
    public bool IsPlayer { get; set; }
    public bool EmailConfirmed { get; set; }

    /// <summary>This row is the signed-in person: they may not change their own role or remove themselves.</summary>
    public bool IsSelf { get; set; }

    /// <summary>This row is the board's only Admin: the board may not be left without one.</summary>
    public bool IsLastAdmin { get; set; }

    /// <summary>Whether the signed-in person may edit this account or resend its welcome email from this board.</summary>
    public AccountReach Reach { get; set; } = AccountReach.Reachable;
}
