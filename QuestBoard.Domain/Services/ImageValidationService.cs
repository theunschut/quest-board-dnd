using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Domain.Services;

internal class ImageValidationService : IImageValidationService
{
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/gif"];
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    /// <inheritdoc/>
    public IList<ImageValidationError> ValidateImagePair(ImageFileInput? original, ImageFileInput? cropped)
    {
        var errors = new List<ImageValidationError>();

        ValidateSingle(original, errors);
        ValidateSingle(cropped, errors);

        return errors;
    }

    // Kept as its own helper (rather than inlined) so a future magic-byte/file-signature check
    // could be added to this one spot later without restructuring the caller.
    private static void ValidateSingle(ImageFileInput? file, List<ImageValidationError> errors)
    {
        // A null or zero-length file is treated as absent -- this phase has no crop UI, so an
        // absent cropped file must never be an error.
        if (file == null || file.Length == 0)
        {
            return;
        }

        if (!AllowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new ImageValidationError(file.FieldName, "Only JPG, PNG, or GIF images are accepted."));
            return;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            errors.Add(new ImageValidationError(file.FieldName, "Only JPG, PNG, or GIF images are accepted."));
            return;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            errors.Add(new ImageValidationError(file.FieldName, "Image cannot exceed 5 MB."));
        }
    }
}
