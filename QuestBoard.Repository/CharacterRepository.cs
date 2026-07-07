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
            .Include(c => c.Classes)
            .OrderByDescending(c => c.Status == 0) // 0 = Active
            .ThenBy(c => c.Owner.Name)
            .ThenBy(c => c.Name)
            .ToListAsync(token);
        var characters = Mapper.Map<IList<Character>>(entities);

        // Image bytes are never selected here -- only a presence flag, via a scalar query that
        // EF Core translates to an EXISTS/JOIN check rather than pulling OriginalImageData/CroppedImageData.
        var imageFlags = await DbContext.Characters
            .Select(c => new { c.Id, HasImage = c.ProfileImage != null })
            .ToDictionaryAsync(x => x.Id, x => x.HasImage, token);
        foreach (var character in characters)
        {
            character.HasProfilePicture = imageFlags.GetValueOrDefault(character.Id);
        }

        return characters;
    }

    /// <inheritdoc/>
    public async Task<IList<Character>> GetCharactersByOwnerIdAsync(int ownerId, CancellationToken token = default)
    {
        var entities = await DbContext.Characters
            .Include(c => c.Owner)
            .Include(c => c.Classes)
            .Where(c => c.OwnerId == ownerId)
            .OrderByDescending(c => c.Role == 0) // 0 = Main
            .ThenByDescending(c => c.Status == 0) // 0 = Active
            .ThenBy(c => c.Name)
            .ToListAsync(token);
        var characters = Mapper.Map<IList<Character>>(entities);

        // Image bytes are never selected here -- only a presence flag, via a scalar query that
        // EF Core translates to an EXISTS/JOIN check rather than pulling OriginalImageData/CroppedImageData.
        var imageFlags = await DbContext.Characters
            .Where(c => c.OwnerId == ownerId)
            .Select(c => new { c.Id, HasImage = c.ProfileImage != null })
            .ToDictionaryAsync(x => x.Id, x => x.HasImage, token);
        foreach (var character in characters)
        {
            character.HasProfilePicture = imageFlags.GetValueOrDefault(character.Id);
        }

        return characters;
    }

    /// <inheritdoc/>
    public async Task<Character?> GetCharacterWithDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await DbContext.Characters
            .Include(c => c.Owner)
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.Id == id, token);
        if (entity == null) return null;

        var character = Mapper.Map<Character>(entity);
        // Image bytes are never selected here -- only a presence flag, via a scalar query that
        // EF Core translates to an EXISTS/JOIN check rather than pulling OriginalImageData/CroppedImageData.
        character.HasProfilePicture = await DbContext.Characters
            .Where(c => c.Id == id)
            .Select(c => c.ProfileImage != null)
            .FirstOrDefaultAsync(token);
        return character;
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
    public async Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default)
    {
        // Rooted at the filtered Characters DbSet (not CharacterImages directly) so the
        // CharacterEntity group filter applies — a cross-group id returns null here.
        return await DbContext.Characters
            .Where(c => c.Id == id)
            .Select(c => c.ProfileImage != null ? c.ProfileImage.OriginalImageData : null)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetCharacterCroppedPictureAsync(int id, CancellationToken token = default)
    {
        // Same group-filtered rooting as the original read; falls back to the original bytes
        // at the query level when no crop has ever been saved for this character.
        return await DbContext.Characters
            .Where(c => c.Id == id)
            .Select(c => c.ProfileImage != null ? c.ProfileImage.CroppedImageData ?? c.ProfileImage.OriginalImageData : null)
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
        // AutoMapper replaced them with) keeps EF's own tracked instances in place. This plain
        // UpdateAsync leaves ProfileImage as whatever is currently tracked/persisted, since image
        // changes for Characters go through UpdateWithProfileImageAsync instead.
        var entity = await DbContext.Characters
            .Include(c => c.Classes)
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == model.Id, token);
        if (entity == null) return;

        var trackedClasses = entity.Classes.ToList();
        var trackedProfileImage = entity.ProfileImage;
        var trackedOwner = entity.Owner;
        var trackedGroup = entity.Group;

        Mapper.Map(model, entity);

        // The domain model's Owner/Group navigations are frequently left unset by callers that
        // only ever populate OwnerId/GroupId (e.g. a freshly-constructed Character with no prior
        // fetch) -- restoring EF's own tracked reference-navigation instances instead of trusting
        // whatever AutoMapper produced from the (possibly null) model.Owner/model.Group avoids
        // nulling out a required FK relationship the tracked entity depends on.
        entity.ProfileImage = trackedProfileImage;
        entity.Owner = trackedOwner;
        entity.Group = trackedGroup;

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
    public async Task UpdateProfileImageAsync(int characterId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default)
    {
        var entity = await DbContext.Characters
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == characterId, token);
        if (entity == null) return;

        ApplyProfileImage(entity, originalImageData, croppedImageData);

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task UpdateWithProfileImageAsync(Character model, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default)
    {
        // Same tracked-entity reconciliation as UpdateAsync, plus the profile image mutation from
        // UpdateProfileImageAsync, saved together in one SaveChangesAsync so a failure partway
        // through cannot durably commit the image while leaving the rest of the entity stale.
        var entity = await DbContext.Characters
            .Include(c => c.Classes)
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == model.Id, token);
        if (entity == null) return;

        var trackedClasses = entity.Classes.ToList();
        var trackedProfileImage = entity.ProfileImage;
        var trackedOwner = entity.Owner;
        var trackedGroup = entity.Group;

        Mapper.Map(model, entity);

        // See UpdateAsync above for why Owner/Group must be restored: the caller-supplied
        // model frequently has no Owner/Group populated (only OwnerId/GroupId), and trusting
        // AutoMapper's mapping of those null references would null out a required FK the
        // tracked entity depends on.
        entity.ProfileImage = trackedProfileImage;
        entity.Owner = trackedOwner;
        entity.Group = trackedGroup;

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

        ApplyProfileImage(entity, originalImageData, croppedImageData);

        await DbContext.SaveChangesAsync(token);
    }

    private static void ApplyProfileImage(CharacterEntity entity, byte[]? originalImageData, byte[]? croppedImageData)
    {
        if (originalImageData == null)
        {
            entity.ProfileImage = null;
        }
        else if (entity.ProfileImage == null)
        {
            entity.ProfileImage = new CharacterImageEntity
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
