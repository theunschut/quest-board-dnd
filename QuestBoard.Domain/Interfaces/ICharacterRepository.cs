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
    /// Returns the raw profile image bytes for a character, or null if none is set.
    /// </summary>
    Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Sets, replaces, or clears (when imageData is null) the character's profile image.
    /// </summary>
    Task UpdateProfileImageAsync(int characterId, byte[]? imageData, CancellationToken token = default);
}
