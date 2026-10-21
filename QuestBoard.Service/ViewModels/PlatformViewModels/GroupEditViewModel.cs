using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    // The posted switches. A page missing from the form keeps its stored value, so an old client
    // that posts only Id and Name changes nothing.
    public List<BoardPageRowViewModel> Pages { get; set; } = [];

    // Null when the form did not post a home page, which leaves the stored home as it is.
    [Display(Name = "Home page")]
    public BoardPage? HomePage { get; set; }

    // The home pages this board's type allows; rebuilt on every render and never read from the form.
    [BindNever]
    public List<SelectListItem> HomeChoices { get; set; } = [];
}
