using System.ComponentModel.DataAnnotations;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class LinkedBoardSetCreateViewModel
{
    [Required(ErrorMessage = "Set name is required.")]
    [StringLength(100, ErrorMessage = "Set name cannot exceed 100 characters.")]
    [Display(Name = "Set name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Board type is required.")]
    [EnumDataType(typeof(BoardType), ErrorMessage = "Select a valid board type.")]
    [Display(Name = "Board type")]
    public BoardType? BoardType { get; set; }
}
