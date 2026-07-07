using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IDungeonMasterProfileRepository : IBaseRepository<DungeonMasterProfile>
{
    /// <summary>
    /// Returns the DM profile for the given user, with its image loaded, or null if none exists.
    /// </summary>
    Task<DungeonMasterProfile?> GetProfileByUserIdAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns the DM's original (unmodified) profile image bytes, or null if none is set.
    /// </summary>
    Task<byte[]?> GetOriginalPictureAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns the cropped/display image, falling back to the original when no crop was ever saved. Null if neither set.
    /// </summary>
    Task<byte[]?> GetCroppedPictureAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Atomically sets, replaces, or clears (when originalImageData is null) both the original and
    /// cropped profile image columns. A null croppedImageData with a non-null originalImageData
    /// writes NULL to the cropped column, clearing any stale crop from a prior upload.
    /// </summary>
    Task UpsertProfileImageAsync(int userId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default);
}
