using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class CreateMemberViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Display Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Group Role")]
    public GroupRole GroupRole { get; set; } = GroupRole.Player;
}
