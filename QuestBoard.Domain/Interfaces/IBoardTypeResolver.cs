using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Resolves the active group's BoardType. Kept separate from IActiveGroupContext
/// because QuestBoardContext's constructor depends on IActiveGroupContext for its
/// tenant query filter — an implementation of this interface that itself needs group
/// data (via IGroupService) would create a circular DI dependency back through
/// QuestBoardContext if it lived on IActiveGroupContext instead.
/// </summary>
public interface IBoardTypeResolver
{
    /// <summary>
    /// Returns the active group's BoardType, or null when no group is active
    /// (or the active group cannot be resolved). Callers must not assume a
    /// default value for the null case — it is a distinct state from OneShot/Campaign.
    /// </summary>
    Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default);
}
