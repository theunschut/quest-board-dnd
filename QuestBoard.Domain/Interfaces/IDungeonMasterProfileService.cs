using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IDungeonMasterProfileService : IBaseService<DungeonMasterProfile>
{
    /// <summary>
    /// Returns the DM profile for the given user, or null if the DM has not yet saved one.
    /// </summary>
    Task<DungeonMasterProfile?> GetProfileByUserIdAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Creates the DM's profile on first save, or updates bio/image on subsequent saves.
    /// imageBytes replaces the stored image; removeImage clears it; both absent leaves the existing image unchanged.
    /// newCroppedImageData, when supplied, is persisted as the new cropped image alongside the original.
    /// </summary>
    Task UpsertProfileAsync(int userId, string? bio, byte[]? imageBytes, bool removeImage = false, byte[]? newCroppedImageData = null, CancellationToken token = default);

    /// <summary>
    /// Returns the DM's original (unmodified) profile image bytes, or null if none is set.
    /// </summary>
    Task<byte[]?> GetProfilePictureAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns the cropped/display image, falling back to the original when no crop was ever saved. Null if neither set.
    /// </summary>
    Task<byte[]?> GetCroppedPictureAsync(int userId, CancellationToken token = default);
}
