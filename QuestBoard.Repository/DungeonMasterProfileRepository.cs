using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class DungeonMasterProfileRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<DungeonMasterProfile, DungeonMasterProfileEntity>(dbContext, mapper), IDungeonMasterProfileRepository
{
    // DungeonMasterProfileEntity uses DatabaseGeneratedOption.None (Id = UserId).
    // Two concurrent first-saves for the same user would both attempt an INSERT
    // for the same PK, causing a DbUpdateException.  Catch and retry with Update
    // to make the upsert safe under concurrent requests — mirrors QuestRepository.AddAsync.
    /// <inheritdoc/>
    public override async Task AddAsync(DungeonMasterProfile model, CancellationToken token = default)
    {
        try
        {
            await base.AddAsync(model, token);
        }
        catch (DbUpdateException)
        {
            // A concurrent request already inserted the row — fall back to an update.
            await UpdateAsync(model, token);
        }
    }

    /// <inheritdoc/>
    public async Task<DungeonMasterProfile?> GetProfileByUserIdAsync(int userId, CancellationToken token = default)
    {
        var entity = await DbContext.DungeonMasterProfiles
            .FirstOrDefaultAsync(p => p.Id == userId, token);
        if (entity == null) return null;

        var profile = Mapper.Map<DungeonMasterProfile>(entity);
        // DM profile images are not group-scoped, so this reads directly from the image
        // DbSet rather than rooting at DungeonMasterProfiles (unlike Character/Contact), matching
        // the sibling read methods in this file. Image bytes are never selected here -- only a
        // presence flag, via a scalar query that EF Core translates to an EXISTS/JOIN check.
        profile.HasProfilePicture = await DbContext.DungeonMasterProfileImages
            .Where(p => p.Id == userId)
            .Select(p => p.OriginalImageData != null)
            .FirstOrDefaultAsync(token);
        return profile;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetOriginalPictureAsync(int userId, CancellationToken token = default)
    {
        // DM profile images are not group-scoped, so this reads directly from the image
        // DbSet rather than rooting at an owner DbSet (unlike Character/Contact).
        return await DbContext.DungeonMasterProfileImages
            .Where(p => p.Id == userId)
            .Select(p => p.OriginalImageData)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetCroppedPictureAsync(int userId, CancellationToken token = default)
    {
        // Falls back to the original bytes at the query level when no crop has ever been saved.
        return await DbContext.DungeonMasterProfileImages
            .Where(p => p.Id == userId)
            .Select(p => p.CroppedImageData ?? p.OriginalImageData)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task UpsertProfileImageAsync(int userId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default)
    {
        var entity = await DbContext.DungeonMasterProfiles
            .Include(p => p.ProfileImage)
            .FirstOrDefaultAsync(p => p.Id == userId, token);
        if (entity == null) return;

        ApplyProfileImage(entity, originalImageData, croppedImageData);

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task UpdateBioWithProfileImageAsync(int userId, string? bio, bool updateImage, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default)
    {
        // Bio and the profile image mutation are saved together in one SaveChangesAsync so a
        // failure partway through cannot durably commit the image while leaving the bio stale.
        // updateImage distinguishes a bio-only edit (image left untouched) from an edit that
        // also sets/replaces/clears the image -- originalImageData alone can't carry that
        // distinction, since "clear the image" and "leave it unchanged" are both represented by
        // a null originalImageData at the call site.
        var entity = await DbContext.DungeonMasterProfiles
            .Include(p => p.ProfileImage)
            .FirstOrDefaultAsync(p => p.Id == userId, token);
        if (entity == null) return;

        entity.Bio = bio;

        if (updateImage)
        {
            ApplyProfileImage(entity, originalImageData, croppedImageData);
        }

        await DbContext.SaveChangesAsync(token);
    }

    private static void ApplyProfileImage(DungeonMasterProfileEntity entity, byte[]? originalImageData, byte[]? croppedImageData)
    {
        if (originalImageData == null)
        {
            entity.ProfileImage = null;
        }
        else if (entity.ProfileImage == null)
        {
            entity.ProfileImage = new DungeonMasterProfileImageEntity
            {
                Id = entity.Id,
                OriginalImageData = originalImageData,
                CroppedImageData = croppedImageData
            };
        }
        else
        {
            entity.ProfileImage.OriginalImageData = originalImageData;
            entity.ProfileImage.CroppedImageData = croppedImageData;
        }
    }
}
