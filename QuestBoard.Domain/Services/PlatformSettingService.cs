using System.Security.Cryptography;
using QuestBoard.Domain.Constants;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class PlatformSettingService(IPlatformSettingRepository repository) : IPlatformSettingService
{
    private static readonly string[] AllKeys =
    [
        PlatformSettingKeys.OmphalosUrl,
        PlatformSettingKeys.OmphalosSharedSecret,
        PlatformSettingKeys.OmphalosEnabled
    ];

    /// <inheritdoc/>
    public async Task<OmphalosSettings> GetResolvedAsync(int? groupId, CancellationToken token = default)
    {
        var url = await repository.GetCascadeValueAsync(PlatformSettingKeys.OmphalosUrl, groupId, token);
        var secret = await repository.GetCascadeValueAsync(PlatformSettingKeys.OmphalosSharedSecret, groupId, token);
        var enabled = await repository.GetCascadeValueAsync(PlatformSettingKeys.OmphalosEnabled, groupId, token);

        return new OmphalosSettings
        {
            Url = url ?? string.Empty,
            SharedSecret = secret,
            IsEnabled = bool.TryParse(enabled, out var e) && e
        };
    }

    /// <inheritdoc/>
    public async Task<OmphalosSettings> GetForScopeAsync(int? groupId, CancellationToken token = default)
    {
        var url = await repository.GetForScopeAsync(PlatformSettingKeys.OmphalosUrl, groupId, token);
        var secret = await repository.GetForScopeAsync(PlatformSettingKeys.OmphalosSharedSecret, groupId, token);
        var enabled = await repository.GetForScopeAsync(PlatformSettingKeys.OmphalosEnabled, groupId, token);

        return new OmphalosSettings
        {
            Url = url?.Value ?? string.Empty,
            SharedSecret = secret?.Value,
            IsEnabled = bool.TryParse(enabled?.Value, out var e) && e
        };
    }

    /// <inheritdoc/>
    public async Task<bool> HasOwnSettingsAsync(int? groupId, CancellationToken token = default)
        => await repository.HasAnyForScopeAsync(groupId, AllKeys, token);

    /// <inheritdoc/>
    public async Task SaveAsync(int? groupId, string url, string? newSecret, bool isEnabled, CancellationToken token = default)
    {
        await repository.UpsertAsync(PlatformSettingKeys.OmphalosUrl, url, groupId, token);
        await repository.UpsertAsync(PlatformSettingKeys.OmphalosEnabled, isEnabled.ToString().ToLowerInvariant(), groupId, token);

        // A blank secret means "leave the currently stored secret untouched" -- skip the
        // upsert entirely so an unrelated edit (e.g. just flipping Enabled) never wipes it.
        if (!string.IsNullOrWhiteSpace(newSecret))
        {
            await repository.UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, newSecret, groupId, token);
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAndSaveSecretAsync(int? groupId, CancellationToken token = default)
    {
        var secret = RandomNumberGenerator.GetHexString(64);
        await repository.UpsertAsync(PlatformSettingKeys.OmphalosSharedSecret, secret, groupId, token);
        return secret;
    }

    /// <inheritdoc/>
    public async Task ClearScopeAsync(int groupId, CancellationToken token = default)
        => await repository.ClearScopeAsync(groupId, AllKeys, token);
}
