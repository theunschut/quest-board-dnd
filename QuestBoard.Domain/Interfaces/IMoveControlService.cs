using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Decides what the move control on an entry's Details page looks like for a viewer.
/// A null result means no control renders at all: there is no active board, or the active
/// board is in no linked set or in a set of one.
/// </summary>
public interface IMoveControlService
{
    Task<MoveControlState?> GetForQuestAsync(Quest quest, int actorUserId, CancellationToken token = default);

    Task<MoveControlState?> GetForEventAsync(Event evt, int actorUserId, CancellationToken token = default);
}
