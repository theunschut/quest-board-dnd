using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

public class EditUserViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(200)]
    [Display(Name = "Email")]
    public string? Email { get; set; }


    [Display(Name = "Has Building Key")]
    public bool HasKey { get; set; }
}