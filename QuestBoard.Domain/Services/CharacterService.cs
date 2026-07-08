using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class CharacterService(ICharacterRepository repository, IMapper mapper) : BaseService<Character>(repository, mapper), ICharacterService
{
    /// <inheritdoc/>
    public async Task<IList<Character>> GetAllCharactersWithDetailsAsync(CancellationToken token = default)
    {
        return await repository.GetAllCharactersWithDetailsAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<Character>> GetCharactersByOwnerIdAsync(int ownerId, CancellationToken token = default)
    {
        return await repository.GetCharactersByOwnerIdAsync(ownerId, token);
    }

    /// <inheritdoc/>
    public async Task<Character?> GetCharacterWithDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetCharacterWithDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task<Character?> GetMainCharacterForUserAsync(int userId, CancellationToken token = default)
    {
        return await repository.GetMainCharacterForUserAsync(userId, token);
    }

    /// <inheritdoc/>
    public async Task SetAsMainCharacterAsync(int characterId, int userId, CancellationToken token = default)
    {
        // Get all user's characters
        var userCharacters = await repository.GetCharactersByOwnerIdAsync(userId, token);

        // Set all to backup
        foreach (var character in userCharacters)
        {
            character.Role = CharacterRole.Backup;
            await repository.UpdateAsync(character, token);
        }

        // Set the selected one to main
        var mainCharacter = userCharacters.FirstOrDefault(c => c.Id == characterId);
        if (mainCharacter != null)
        {
            mainCharacter.Role = CharacterRole.Main;
            await repository.UpdateAsync(mainCharacter, token);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ValidateCharacterClassLevelsAsync(int totalLevel, IList<CharacterClass> classes)
    {
        if (!classes.Any())
            return Task.FromResult(false);

        var sumOfClassLevels = classes.Sum(c => c.ClassLevel);
        return Task.FromResult(sumOfClassLevels == totalLevel);
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(Character model, CancellationToken token = default)
    {
        // No caller-supplied signal (e.g. a not-yet-updated call site) defaults to the safe
        // preserve-crop behaviour, since model.ProfilePicture is never null on a no-photo-change edit.
        await UpdateAsync(model, hasNewOriginalUpload: false, token);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default) =>
        UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData: null, token);

    /// <inheritdoc/>
    public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default)
    {
        // The image write and the rest of the entity's fields are saved together in a single
        // repository call so a failure in either half cannot leave the character in a
        // half-updated state (new photo, stale metadata, or vice versa).
        byte[]? croppedImageData;
        if (newCroppedImageData != null)
        {
            // The caller submitted a genuinely new crop this request -- persist it directly.
            croppedImageData = newCroppedImageData;
        }
        else if (hasNewOriginalUpload)
        {
            // A genuinely new original arrived this request with no accompanying crop -- clear
            // any stale crop of the superseded photo, since it belonged to the photo that's
            // being replaced.
            croppedImageData = null;
        }
        else
        {
            // No new file; model.ProfilePicture is the round-tripped existing original. Fetch
            // the currently-stored crop and pass it through unchanged so it survives an
            // unrelated-field edit.
            croppedImageData = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
        }

        // The original follows the same "don't trust the round-tripped value" rule as the
        // crop above: the read path that populates model.ProfilePicture no longer loads the
        // original's bytes, so on a no-upload edit it must be re-fetched fresh here rather
        // than passed straight through -- otherwise an unrelated-field edit would wipe the
        // stored original image.
        var originalImageData = hasNewOriginalUpload
            ? model.ProfilePicture
            : await repository.GetCharacterOriginalPictureAsync(model.Id, token);

        await repository.UpdateWithProfileImageAsync(model, originalImageData, croppedImageData, token);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Character model, byte[]? newCroppedImageData, CancellationToken token = default)
    {
        // The base Add call creates the character and its profile image row (original only).
        await repository.AddAsync(model, token);

        // Only make a second write when the caller actually submitted a crop this request --
        // otherwise the freshly-created row is left exactly as the base Add produced it.
        if (newCroppedImageData != null)
        {
            await repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, newCroppedImageData, token);
        }
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetCharacterOriginalPictureAsync(int id, CancellationToken token = default)
    {
        return repository.GetCharacterOriginalPictureAsync(id, token);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetCharacterCroppedPictureAsync(int id, CancellationToken token = default)
    {
        return repository.GetCharacterCroppedPictureAsync(id, token);
    }
}
