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
    public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default)
    {
        if (hasNewOriginalUpload)
        {
            // A genuinely new original arrived this request -- clear any stale crop of the
            // superseded photo (Pitfall 5).
            await repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, croppedImageData: null, token);
        }
        else
        {
            // No new file; model.ProfilePicture is the round-tripped existing original. Fetch
            // the currently-stored crop and pass it through unchanged so it survives an
            // unrelated-field edit (Pitfall 4).
            var existingCropped = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
            await repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, existingCropped, token);
        }

        await repository.UpdateAsync(model, token);
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
