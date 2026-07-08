using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    // Board type is set once at creation and never changes; the controller assigns this
    // for display only (GET), and it must never be populated from the posted form (POST).
    [Display(Name = "Board Type")]
    [BindNever]
    public BoardType BoardType { get; set; }
}
