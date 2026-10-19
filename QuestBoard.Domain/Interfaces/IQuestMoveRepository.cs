using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// The one unit of work that moves a quest to another board.
/// </summary>
public interface IQuestMoveRepository
{
    /// <summary>
    /// Applies the plan in a single save. Returns <see cref="MoveStatus.Moved"/>,
    /// <see cref="MoveStatus.NotFound"/> when the quest is not on the source board, or
    /// <see cref="MoveStatus.Changed"/> when the quest's signups no longer match the plan.
    /// </summary>
    Task<MoveStatus> ApplyAsync(QuestMovePlan plan, CancellationToken token = default);
}
