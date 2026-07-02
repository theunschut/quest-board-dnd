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
    /// Returns the raw profile image bytes for a character, or null if none is set.
    /// </summary>
    Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default);
}
