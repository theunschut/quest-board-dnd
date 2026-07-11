using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Scope-oriented settings service for the Omphalos integration trio. Deliberately not
/// IBaseService&lt;PlatformSetting&gt; — a key-value settings surface is resolved by (key,
/// scope), not by Id.
/// </summary>
public interface IPlatformSettingService
{
    /// <summary>
    /// Returns the cascade-resolved trio for the given scope (group override falling back to
    /// the instance-wide default). Consumed by the token generator and by the group page to
    /// compute inherited status.
    /// </summary>
    Task<OmphalosSettings> GetResolvedAsync(int? groupId, CancellationToken token = default);

    /// <summary>
    /// Returns the scope's own three settings only, with no fallback to the instance-wide
    /// default. Used to pre-fill an edit form for the scope.
    /// </summary>
    Task<OmphalosSettings> GetForScopeAsync(int? groupId, CancellationToken token = default);

    /// <summary>
    /// Returns whether the scope has any of its own rows for the three settings.
    /// </summary>
    Task<bool> HasOwnSettingsAsync(int? groupId, CancellationToken token = default);

    /// <summary>
    /// Upserts the three keys for the scope. When newSecret is null or whitespace, the stored
    /// secret is preserved unchanged rather than being overwritten with an empty value.
    /// </summary>
    Task SaveAsync(int? groupId, string url, string? newSecret, bool isEnabled, CancellationToken token = default);

    /// <summary>
    /// Generates a cryptographically random secret, persists it immediately for the scope, and
    /// returns the plaintext value once.
    /// </summary>
    Task<string> GenerateAndSaveSecretAsync(int? groupId, CancellationToken token = default);

    /// <summary>
    /// Deletes a group's own rows for all three keys (group-override clear). The instance
    /// default has nothing above it to clear.
    /// </summary>
    Task ClearScopeAsync(int groupId, CancellationToken token = default);
}
