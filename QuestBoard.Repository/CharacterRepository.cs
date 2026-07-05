using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class CharacterRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<Character, CharacterEntity>(dbContext, mapper), ICharacterRepository
{
    /// <inheritdoc/>
    public async Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default)
    {
        var entities = await DbContext.Characters
            .Include(c => c.Owner)
            .Include(c => c.ProfileImage)
            .Include(c => c.Classes)
            .OrderByDescending(c => c.Status == 0) // 0 = Active
            .ThenBy(c => c.Owner.Name)
            .ThenBy(c => c.Name)
            .ToListAsync(token);
        return Mapper.Map<IList<Character>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Character>> GetCharactersByOwnerIdAsync(int ownerId, CancellationToken token = default)
    {
        var entities = await DbContext.Characters
            .Include(c => c.Owner)
            .Include(c => c.ProfileImage)
            .Include(c => c.Classes)
            .Where(c => c.OwnerId == ownerId)
            .OrderByDescending(c => c.Role == 0) // 0 = Main
            .ThenByDescending(c => c.Status == 0) // 0 = Active
            .ThenBy(c => c.Name)
            .ToListAsync(token);
        return Mapper.Map<IList<Character>>(entities);
    }

    /// <inheritdoc/>
    public async Task<Character?> GetCharacterWithDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await DbContext.Characters
            .Include(c => c.Owner)
            .Include(c => c.ProfileImage)
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.Id == id, token);
        return entity == null ? null : Mapper.Map<Character>(entity);
    }

    /// <inheritdoc/>
    public async Task<Character?> GetMainCharacterForUserAsync(int userId, CancellationToken token = default)
    {
        var entity = await DbContext.Characters
            .Include(c => c.ProfileImage)
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.OwnerId == userId && c.Role == 0, token); // 0 = Main
        return entity == null ? null : Mapper.Map<Character>(entity);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default)
    {
        // Rooted at the filtered Characters DbSet (not CharacterImages directly) so the
        // CharacterEntity group filter applies — a cross-group id returns null here.
        return await DbContext.Characters
            .Where(c => c.Id == id)
            .Select(c => c.ProfileImage != null ? c.ProfileImage.ImageData : null)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task UpdateProfileImageAsync(int characterId, byte[]? imageData, CancellationToken token = default)
    {
        var entity = await DbContext.Characters
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == characterId, token);
        if (entity == null) return;

        if (imageData == null)
        {
            entity.ProfileImage = null;
        }
        else if (entity.ProfileImage == null)
        {
            entity.ProfileImage = new CharacterImageEntity
            {
                Id = entity.Id,
                ImageData = imageData
            };
        }
        else
        {
            entity.ProfileImage.ImageData = imageData;
        }

        await DbContext.SaveChangesAsync(token);
    }
}
