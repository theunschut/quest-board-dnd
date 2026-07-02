using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class DungeonMasterProfileService(IDungeonMasterProfileRepository repository, IMapper mapper)
    : BaseService<DungeonMasterProfile>(repository, mapper), IDungeonMasterProfileService
{
    /// <inheritdoc/>
    public async Task<DungeonMasterProfile?> GetProfileByUserIdAsync(int userId, CancellationToken token = default)
    {
        return await repository.GetProfileByUserIdAsync(userId, token);
    }

    /// <inheritdoc/>
    public async Task UpsertProfileAsync(int userId, string? bio, byte[]? imageBytes, bool removeImage = false, CancellationToken token = default)
    {
        // imageBytes != null  → replace stored image with new bytes
        // removeImage == true  → explicitly clear the stored image (e.g. "remove photo" button)
        // both false/null     → keep existing image unchanged (bio-only edit)
        var profile = await repository.GetProfileByUserIdAsync(userId, token);
        if (profile == null)
        {
            // Lazy create — profile entity does not exist until DM first saves
            var newProfile = new DungeonMasterProfile { Id = userId, Bio = bio };
            await repository.AddAsync(newProfile, token);
            if (imageBytes != null)
                await repository.UpsertProfileImageAsync(userId, imageBytes, token);
        }
        else
        {
            profile.Bio = bio;
            await repository.UpdateAsync(profile, token);
            if (imageBytes != null || removeImage)
                await repository.UpsertProfileImageAsync(userId, removeImage ? null : imageBytes, token);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetProfilePictureAsync(int userId, CancellationToken token = default)
    {
        return await repository.GetProfilePictureAsync(userId, token);
    }
}
