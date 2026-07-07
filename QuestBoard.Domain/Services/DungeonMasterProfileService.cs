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
        // imageBytes != null  → replace stored image with new bytes; also clears any stale
        //                        cropped image from a prior upload (Pitfall 5), since imageBytes
        //                        being non-null here already means a genuinely new original arrived.
        // removeImage == true  → explicitly clear the stored image (e.g. "remove photo" button)
        // both false/null     → keep existing image unchanged (bio-only edit) -- the crop is
        //                        preserved by construction, since UpsertProfileImageAsync is
        //                        never called on this path (Pitfall 4).
        var profile = await repository.GetProfileByUserIdAsync(userId, token);
        if (profile == null)
        {
            // Lazy create — profile entity does not exist until DM first saves
            var newProfile = new DungeonMasterProfile { Id = userId, Bio = bio };
            await repository.AddAsync(newProfile, token);
            if (imageBytes != null)
                await repository.UpsertProfileImageAsync(userId, imageBytes, croppedImageData: null, token);
        }
        else
        {
            profile.Bio = bio;
            await repository.UpdateAsync(profile, token);
            if (imageBytes != null || removeImage)
                await repository.UpsertProfileImageAsync(userId, removeImage ? null : imageBytes, croppedImageData: null, token);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetProfilePictureAsync(int userId, CancellationToken token = default)
    {
        return await repository.GetOriginalPictureAsync(userId, token);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetCroppedPictureAsync(int userId, CancellationToken token = default)
    {
        return await repository.GetCroppedPictureAsync(userId, token);
    }
}
