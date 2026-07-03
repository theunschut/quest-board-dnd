using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Provides the active group ID for the current request or execution context.
/// Null means "see all records".
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }

    /// <summary>
    /// Returns the active group's BoardType, or null when no group is active
    /// (or the active group cannot be resolved). Callers must not assume a
    /// default value for the null case — it is a distinct state from OneShot/Campaign.
    /// </summary>
    Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default);
}
