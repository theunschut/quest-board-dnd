namespace QuestBoard.Domain.Interfaces;

public interface IImageValidationService
{
    /// <summary>
    /// Validates an original (required-if-present) plus an optional cropped image pair against the
    /// shared MIME allowlist, extension allowlist, and size limit. A null or zero-length file is
    /// treated as absent and never produces an error. Returns errors keyed by field name (empty
    /// list means both files are valid).
    /// </summary>
    IList<ImageValidationError> ValidateImagePair(ImageFileInput? original, ImageFileInput? cropped);
}

/// <summary>
/// The subset of an uploaded file's metadata needed for validation, kept as primitive values
/// (not IFormFile) so the validator can be unit-tested without constructing upload fakes and so
/// the Domain layer stays free of a hard dependency on ASP.NET Core upload types.
/// </summary>
public record ImageFileInput(long Length, string ContentType, string FileName, string FieldName);

public record ImageValidationError(string FieldName, string Message);
