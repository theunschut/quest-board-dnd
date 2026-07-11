using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IPlatformSettingRepository : IBaseRepository<PlatformSetting>
{
    /// <summary>
    /// Returns the exact (key, groupId) row, with no fallback to the instance-wide default.
    /// </summary>
    Task<PlatformSetting?> GetForScopeAsync(string key, int? groupId, CancellationToken token = default);

    /// <summary>
    /// Resolves a setting's value by cascade: the group's own override first, falling back to
    /// the instance-wide default (GroupId == null) when no override exists for that key/group.
    /// Returns null when neither a group override nor an instance default exists.
    /// </summary>
    Task<string?> GetCascadeValueAsync(string key, int? groupId, CancellationToken token = default);

    /// <summary>
    /// Inserts or updates the single (key, groupId) row with the given value.
    /// </summary>
    Task UpsertAsync(string key, string value, int? groupId, CancellationToken token = default);

    /// <summary>
    /// Returns whether any of the given keys has a row at that exact scope (used to decide
    /// Override Active vs Inherited on the group override page).
    /// </summary>
    Task<bool> HasAnyForScopeAsync(int? groupId, IEnumerable<string> keys, CancellationToken token = default);

    /// <summary>
    /// Deletes the given keys' rows at that exact scope, if they exist.
    /// </summary>
    Task ClearScopeAsync(int? groupId, IEnumerable<string> keys, CancellationToken token = default);
}
