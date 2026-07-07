using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ICharacterRepository : IBaseRepository<Character>
{
    /// <summary>
    /// Returns all characters with owner, profile image, and class details loaded, active characters first.
    /// </summary>
    Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all characters owned by the given user, main/active characters first.
    /// </summary>
    Task<IList<Character>> GetCharactersByOwnerIdAsync(int ownerId, CancellationToken token = default);

    /// <summary>
    /// Returns a single character with owner, profile image, and class details loaded.
    /// </summary>
    Task<Character?> GetCharacterWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the user's character currently marked as Main, or null if none is set.
    /// </summary>
    Task<Character?> GetMainCharacterForUserAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns the character's original (unmodified) profile image bytes, or null if none is set.
    /// </summary>
    Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the cropped/display image, falling back to the original when no crop was ever saved. Null if neither set.
    /// </summary>
    Task<byte[]?> GetCharacterCroppedPictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Atomically sets, replaces, or clears (when originalImageData is null) both the original and
    /// cropped profile image columns. A null croppedImageData with a non-null originalImageData
    /// writes NULL to the cropped column, clearing any stale crop from a prior upload.
    /// </summary>
    Task UpdateProfileImageAsync(int characterId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default);

    /// <summary>
    /// Updates a character's scalar fields and its profile image in a single save, so a failure in
    /// either half (e.g. a concurrency conflict on the entity update) cannot leave the image durably
    /// committed while the rest of the character's fields are left stale, or vice versa.
    /// </summary>
    Task UpdateWithProfileImageAsync(Character model, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default);
}
