using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Moves a quest from the active board to another board in its linked set.
/// </summary>
public interface IQuestMoveService
{
    /// <summary>
    /// Moves the quest to the target board. The returned preview's status is the outcome.
    /// </summary>
    Task<QuestMovePreview> MoveAsync(int questId, int targetBoardId, int actorUserId, CancellationToken token = default);
}
