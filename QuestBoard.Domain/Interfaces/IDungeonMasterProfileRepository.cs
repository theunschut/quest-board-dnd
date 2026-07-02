using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IDungeonMasterProfileRepository : IBaseRepository<DungeonMasterProfile>
{
    /// <summary>
    /// Returns the DM profile for the given user, with its image loaded, or null if none exists.
    /// </summary>
    Task<DungeonMasterProfile?> GetProfileByUserIdAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns the raw profile image bytes for a DM, or null if none is set.
    /// </summary>
    Task<byte[]?> GetProfilePictureAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Sets, replaces, or clears (when imageData is null) the DM profile's image.
    /// </summary>
    Task UpsertProfileImageAsync(int userId, byte[]? imageData, CancellationToken token = default);
}
