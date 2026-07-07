using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.ContactViewModels;

public class ContactViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Contact name is required")]
    [StringLength(100, ErrorMessage = "Contact name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [StringLength(200, ErrorMessage = "Town/city cannot exceed 200 characters")]
    public string? TownCity { get; set; }

    [StringLength(200, ErrorMessage = "Sub-location cannot exceed 200 characters")]
    public string? SubLocation { get; set; }

    public bool HasContactImage { get; set; }

    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Image cannot exceed 5 MB")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
    public IFormFile? ContactImageFile { get; set; }

    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Cropped image cannot exceed 5 MB")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
    public IFormFile? CroppedPictureFile { get; set; }

    public bool IsRevealed { get; set; }

    public int CreatedByUserId { get; set; }

    // DM-tier viewer flag. There is no owner concept for Contacts, so this single flag
    // (rather than an IsOwner/CanEdit pair) drives Edit/Delete/Reveal button visibility.
    public bool CanManage { get; set; }

    public List<ContactNoteViewModel> Notes { get; set; } = [];
}

public class ContactNoteViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(2000, ErrorMessage = "Note cannot exceed 2000 characters")]
    public string Text { get; set; } = string.Empty;

    public string? AuthorName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

// Custom validation attributes, mirroring QuestBoard.Service.ViewModels.CharacterViewModels
// so Contact image uploads follow the exact same size/extension rules.
public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly int _maxFileSize;

    public MaxFileSizeAttribute(int maxFileSize)
    {
        _maxFileSize = maxFileSize;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            if (file.Length > _maxFileSize)
            {
                var maxSizeMB = _maxFileSize / 1024.0 / 1024.0;
                return new ValidationResult($"File size cannot exceed {maxSizeMB:F1} MB");
            }
        }

        return ValidationResult.Success;
    }
}

public class AllowedExtensionsAttribute : ValidationAttribute
{
    private readonly string[] _extensions;

    public AllowedExtensionsAttribute(string[] extensions)
    {
        _extensions = extensions;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_extensions.Contains(extension))
            {
                return new ValidationResult($"Only {string.Join(", ", _extensions)} files are allowed");
            }
        }

        return ValidationResult.Success;
    }
}
