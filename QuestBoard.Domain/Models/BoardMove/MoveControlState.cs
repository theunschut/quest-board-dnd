using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// What the Details page's move control should show on a linked board: enabled when nothing
/// blocks the entry, otherwise disabled with the reason.
/// </summary>
public record MoveControlState(MoveBlockReason BlockReason)
{
    public bool CanMove => BlockReason == MoveBlockReason.None;
}
