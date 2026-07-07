using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ICharacterService : IBaseService<Character>
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
    /// Sets the given character as the user's Main character, demoting all of the user's other characters to Backup.
    /// </summary>
    Task SetAsMainCharacterAsync(int characterId, int userId, CancellationToken token = default);

    /// <summary>
    /// Returns whether the sum of the given classes' levels equals the expected total character level.
    /// </summary>
    Task<bool> ValidateCharacterClassLevelsAsync(int totalLevel, IList<CharacterClass> classes);

    /// <summary>
    /// Returns the character's original (unmodified) profile image bytes, or null if none is set.
    /// </summary>
    Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the cropped/display image, falling back to the original when no crop was ever saved. Null if neither set.
    /// </summary>
    Task<byte[]?> GetCharacterCroppedPictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Updates a character, threading an explicit signal for whether a genuinely new original
    /// image was uploaded this request. hasNewOriginalUpload == true clears any stale cropped
    /// image (a new original supersedes the old crop); false preserves the existing crop
    /// unchanged, since model.ProfilePicture is never null on a no-photo-change edit.
    /// </summary>
    Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default);

    /// <summary>
    /// Updates a character, threading both the new-original-upload signal and a caller-supplied
    /// cropped image. A non-null newCroppedImageData is persisted directly; when null, falls back
    /// to the same hasNewOriginalUpload-driven clear-or-preserve resolution as the 3-arg overload.
    /// </summary>
    Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default);

    /// <summary>
    /// Creates a character, then persists a caller-supplied cropped image immediately after,
    /// so a crop chosen at creation time isn't silently dropped. A null newCroppedImageData
    /// behaves identically to the plain AddAsync.
    /// </summary>
    Task AddAsync(Character model, byte[]? newCroppedImageData, CancellationToken token = default);
}
