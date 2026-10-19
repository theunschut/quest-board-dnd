using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Moves a quest from the active board to another board in its linked set.
/// </summary>
public interface IQuestMoveService
{
    /// <summary>
    /// Works out what moving the quest would do. With no target, a single other board is
    /// preselected and several ask the caller to choose. Nothing is written.
    /// </summary>
    Task<QuestMovePreview> PreviewAsync(int questId, int? targetBoardId, int actorUserId, CancellationToken token = default);

    /// <summary>
    /// Recomputes the preview and moves the quest only when it is ready and the posted stamp
    /// still matches. The returned preview's status is the outcome.
    /// </summary>
    Task<QuestMovePreview> MoveAsync(int questId, int targetBoardId, string stamp, int actorUserId, CancellationToken token = default);
}
