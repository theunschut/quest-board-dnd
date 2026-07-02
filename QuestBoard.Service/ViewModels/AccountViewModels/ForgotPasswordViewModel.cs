using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AccountViewModels;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
