using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Services;

/// <summary>
/// Resolves the active group's BoardType via a single group lookup.
/// Kept separate from IActiveGroupContext/ActiveGroupContextService because this
/// service depends on IGroupService, whose repository chain depends on
/// QuestBoardContext — which itself depends on IActiveGroupContext. Putting this
/// lookup on ActiveGroupContextService would create a circular DI dependency.
/// </summary>
public class BoardTypeResolver(IActiveGroupContext activeGroupContext, IGroupService groupService) : IBoardTypeResolver
{
    /// <summary>
    /// Returns null when no group is active or the active group cannot be found —
    /// callers (e.g. nav visibility gating) must treat null as its own state, not OneShot.
    /// </summary>
    public async Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default)
    {
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            return null;
        }

        var group = await groupService.GetByIdAsync(groupId, token);
        return group?.BoardType;
    }
}
