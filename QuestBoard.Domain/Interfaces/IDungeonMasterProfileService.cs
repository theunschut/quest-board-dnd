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
    /// </summary>
    Task UpsertProfileAsync(int userId, string? bio, byte[]? imageBytes, bool removeImage = false, CancellationToken token = default);

    /// <summary>
    /// Returns the raw profile image bytes for a DM, or null if none is set.
    /// </summary>
    Task<byte[]?> GetProfilePictureAsync(int userId, CancellationToken token = default);
}
