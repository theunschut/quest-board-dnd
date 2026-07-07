using QuestBoard.Service.ViewModels.CharacterViewModels;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.DungeonMasterViewModels;

public class EditDMProfileViewModel
{
    public int DungeonMasterId { get; set; }

    [StringLength(2000, ErrorMessage = "Bio cannot exceed 2000 characters")]
    public string? Bio { get; set; }

    public byte[]? ProfilePicture { get; set; }   // populated from DB; drives current-image display

    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Profile picture cannot exceed 5 MB")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
    public IFormFile? ProfilePictureFile { get; set; }

    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Cropped image cannot exceed 5 MB")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
    public IFormFile? CroppedPictureFile { get; set; }
}
