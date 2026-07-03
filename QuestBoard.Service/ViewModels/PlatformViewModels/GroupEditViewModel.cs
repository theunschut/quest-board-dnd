using System.ComponentModel.DataAnnotations;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Board Type")]
    public BoardType BoardType { get; set; }
}
