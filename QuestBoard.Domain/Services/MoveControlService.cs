using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Services;

internal class MoveControlService(
    ILinkedBoardSetRepository linkedBoardSetRepository,
    IActiveGroupContext activeGroupContext,
    IBoardClock boardClock) : IMoveControlService
{
    /// <inheritdoc/>
    public async Task<MoveControlState?> GetForQuestAsync(Quest quest, int actorUserId, CancellationToken token = default) =>
        await HasLinkedBoardTakingAsync(actorUserId, BoardPage.Quests, token)
            ? new MoveControlState(MoveEligibility.ForQuest(quest, boardClock.Today))
            : null;

    /// <inheritdoc/>
    public async Task<MoveControlState?> GetForEventAsync(Event evt, int actorUserId, CancellationToken token = default) =>
        await HasLinkedBoardTakingAsync(actorUserId, BoardPage.Events, token)
            ? new MoveControlState(MoveEligibility.ForEvent(evt, boardClock.Today))
            : null;

    // The set is read fresh on every call so a link made or removed a moment ago is honoured. The
    // control is only worth offering when some linked board uses this kind of entry.
    private async Task<bool> HasLinkedBoardTakingAsync(int actorUserId, BoardPage kind, CancellationToken token)
    {
        if (activeGroupContext.ActiveGroupId is not { } activeGroupId)
        {
            return false;
        }

        var linked = await linkedBoardSetRepository.GetLinkedBoardsForViewerAsync(activeGroupId, actorUserId, token);
        return linked.Boards.Any(b => BoardPagePolicy.IsAvailable(linked.SetBoardType, b.DisabledPages, kind));
    }
}
