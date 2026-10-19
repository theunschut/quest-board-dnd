using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// The one unit of work that moves an event to another board.
/// </summary>
public interface IEventMoveRepository
{
    /// <summary>
    /// Applies the plan in a single save. Returns <see cref="MoveStatus.Moved"/>,
    /// <see cref="MoveStatus.NotFound"/> when the event is not on the source board, or
    /// <see cref="MoveStatus.Changed"/> when the event's availability rows no longer match the plan.
    /// </summary>
    Task<MoveStatus> ApplyAsync(EventMovePlan plan, CancellationToken token = default);
}
