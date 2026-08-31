using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

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

    // The only one of the five category members below that is bound from a form post.
    public int? CategoryId { get; set; }

    // Display-only values carried from the mapped category; never posted back.
    public string? CategoryName { get; set; }

    public int? CategorySortOrder { get; set; }

    // Populated by the controller for form rendering; never posted back.
    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];

    public bool HasCategories { get; set; }

    // Display list for the chips and the details line.
    public IList<ContactTagViewModel> Tags { get; set; } = [];

    // The bound comma-separated field on the create and edit forms. This bounds one request's
    // payload; it is deliberately not a cap on tags per contact, which stays uncapped, and 1000
    // characters comfortably holds several dozen names.
    [StringLength(1000, ErrorMessage = "Tags cannot exceed 1000 characters in total")]
    public string? TagsInput { get; set; }

    // The suggestion list handed to the client-side tag widget.
    public IList<string> AvailableTagNames { get; set; } = [];
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
