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
            .Select(c => c.ProfileImage != null ? c.ProfileImage.OriginalImageData : null)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(Character model, CancellationToken token = default)
    {
        // AutoMapper's default mapping replaces child navigations (Classes, ProfileImage) with
        // brand-new CLR instances rather than mutating the entities EF is already tracking —
        // even when the new instances carry the same Id as an existing row. EF's change
        // tracker then treats those as detached objects it never queried, which the InMemory
        // provider rejects with a concurrency exception on save. Loading both navigations
        // explicitly and reconciling them by Id afterward (instead of trusting whatever
        // AutoMapper replaced them with) keeps EF's own tracked instances in place. ProfileImage
        // itself is already persisted correctly by the prior UpdateProfileImageAsync call in
        // CharacterService.UpdateAsync, so it is restored here rather than re-derived.
        var entity = await DbContext.Characters
            .Include(c => c.Classes)
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == model.Id, token);
        if (entity == null) return;

        var trackedClasses = entity.Classes.ToList();
        var trackedProfileImage = entity.ProfileImage;

        Mapper.Map(model, entity);

        entity.ProfileImage = trackedProfileImage;

        var incomingClasses = Mapper.Map<List<CharacterClassEntity>>(model.Classes);
        var incomingIds = incomingClasses.Where(c => c.Id != 0).Select(c => c.Id).ToHashSet();

        entity.Classes.Clear();
        foreach (var stale in trackedClasses.Where(c => !incomingIds.Contains(c.Id)))
        {
            DbContext.Remove(stale);
        }

        foreach (var incoming in incomingClasses)
        {
            var tracked = incoming.Id != 0
                ? trackedClasses.FirstOrDefault(c => c.Id == incoming.Id)
                : null;

            if (tracked != null)
            {
                tracked.Class = incoming.Class;
                tracked.ClassLevel = incoming.ClassLevel;
                entity.Classes.Add(tracked);
            }
            else
            {
                incoming.Id = 0;
                incoming.CharacterId = entity.Id;
                entity.Classes.Add(incoming);
            }
        }

        await DbContext.SaveChangesAsync(token);
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
