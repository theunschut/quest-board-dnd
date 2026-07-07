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
    public async Task UpsertProfileAsync(int userId, string? bio, byte[]? imageBytes, bool removeImage = false, byte[]? newCroppedImageData = null, CancellationToken token = default)
    {
        // imageBytes != null  → replace stored image with new bytes; also clears any stale
        //                        cropped image from a prior upload, since imageBytes being
        //                        non-null here already means a genuinely new original arrived --
        //                        unless newCroppedImageData supplies a real replacement crop.
        // removeImage == true  → explicitly clear the stored image (e.g. "remove photo" button)
        // both false/null     → keep existing image unchanged (bio-only edit) -- the crop is
        //                        preserved by construction, since the image is never touched on
        //                        this path.
        //
        // Bio and the image mutation (when one is needed) are saved together in a single
        // repository call so a failure in either half cannot leave the profile in a
        // half-updated state (new photo, stale bio, or vice versa).
        var profile = await repository.GetProfileByUserIdAsync(userId, token);
        var updateImage = imageBytes != null || removeImage;
        if (profile == null)
        {
            // Lazy create — profile entity does not exist until DM first saves. AddAsync must
            // run first (DatabaseGeneratedOption.None means Id = userId has to exist as a row
            // before the image FK can be attached), but the bio it writes is identical to the
            // bio written by the follow-up call below, so a failure of the second call still
            // leaves the profile in a valid (bio-only) state rather than a half-updated one.
            var newProfile = new DungeonMasterProfile { Id = userId, Bio = bio };
            await repository.AddAsync(newProfile, token);
            if (updateImage)
                await repository.UpdateBioWithProfileImageAsync(userId, bio, updateImage: true, removeImage ? null : imageBytes, croppedImageData: newCroppedImageData, token);
        }
        else
        {
            await repository.UpdateBioWithProfileImageAsync(userId, bio, updateImage, removeImage ? null : imageBytes, croppedImageData: newCroppedImageData, token);
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
