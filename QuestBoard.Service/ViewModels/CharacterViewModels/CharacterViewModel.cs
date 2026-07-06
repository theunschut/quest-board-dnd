using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.CharacterViewModels;

public class CharacterViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Character name is required")]
    [StringLength(100, ErrorMessage = "Character name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 20, ErrorMessage = "Level must be between 1 and 20")]
    public int Level { get; set; } = 1;

    [StringLength(500, ErrorMessage = "Sheet link cannot exceed 500 characters")]
    [Url(ErrorMessage = "Please enter a valid URL")]
    public string? SheetLink { get; set; }

    public CharacterStatus Status { get; set; } = CharacterStatus.Active;

    public CharacterRole Role { get; set; } = CharacterRole.Backup;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; set; }

    [StringLength(5000, ErrorMessage = "Backstory cannot exceed 5000 characters")]
    public string? Backstory { get; set; }

    public int OwnerId { get; set; }

    public string? OwnerName { get; set; }

    public byte[]? ProfilePicture { get; set; }

    [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Profile picture cannot exceed 5 MB")]
    [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
    public IFormFile? ProfilePictureFile { get; set; }

    public List<CharacterClassViewModel> Classes { get; set; } = [];

    public bool IsOwner { get; set; }

    public bool CanEdit { get; set; }
}

public class CharacterClassViewModel
{
    public int Id { get; set; }

    [Required]
    public DndClass Class { get; set; }

    [Range(1, 20, ErrorMessage = "Class level must be between 1 and 20")]
    public int ClassLevel { get; set; } = 1;
}

// Custom validation attributes
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
