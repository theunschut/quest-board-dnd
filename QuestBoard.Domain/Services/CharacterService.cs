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
        await repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, token);
        await repository.UpdateAsync(model, token);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetCharacterProfilePictureAsync(int id, CancellationToken token = default)
    {
        return repository.GetCharacterProfilePictureAsync(id, token);
    }
}
