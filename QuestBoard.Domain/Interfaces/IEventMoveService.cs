using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Moves a one-off event from the active board to another board in its linked set.
/// </summary>
public interface IEventMoveService
{
    /// <summary>
    /// Works out what moving the event would do. With no target, a single other board is
    /// preselected and several ask the caller to choose. Nothing is written.
    /// </summary>
    Task<EventMovePreview> PreviewAsync(int eventId, int? targetBoardId, int actorUserId, CancellationToken token = default);

    /// <summary>
    /// Recomputes the preview and moves the event only when it is ready and the posted stamp
    /// still matches. The returned preview's status is the outcome. Nobody is emailed.
    /// </summary>
    Task<EventMovePreview> MoveAsync(int eventId, int targetBoardId, string stamp, int actorUserId, CancellationToken token = default);
}
