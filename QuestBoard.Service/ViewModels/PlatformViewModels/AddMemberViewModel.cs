using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class AddMemberViewModel
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a user.")]
    public int UserId { get; set; }

    [Required]
    public GroupRole Role { get; set; }
}
