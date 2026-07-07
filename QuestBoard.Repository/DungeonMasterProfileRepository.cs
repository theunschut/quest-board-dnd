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
            .Include(p => p.ProfileImage)
            .FirstOrDefaultAsync(p => p.Id == userId, token);
        return entity == null ? null : Mapper.Map<DungeonMasterProfile>(entity);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetProfilePictureAsync(int userId, CancellationToken token = default)
    {
        return await DbContext.DungeonMasterProfileImages
            .Where(p => p.Id == userId)
            .Select(p => p.OriginalImageData)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task UpsertProfileImageAsync(int userId, byte[]? imageData, CancellationToken token = default)
    {
        var entity = await DbContext.DungeonMasterProfiles
            .Include(p => p.ProfileImage)
            .FirstOrDefaultAsync(p => p.Id == userId, token);
        if (entity == null) return;

        if (imageData == null)
        {
            entity.ProfileImage = null;
        }
        else if (entity.ProfileImage == null)
        {
            entity.ProfileImage = new DungeonMasterProfileImageEntity
            {
                Id = entity.Id,
                OriginalImageData = imageData
            };
        }
        else
        {
            entity.ProfileImage.OriginalImageData = imageData;
        }

        await DbContext.SaveChangesAsync(token);
    }
}
