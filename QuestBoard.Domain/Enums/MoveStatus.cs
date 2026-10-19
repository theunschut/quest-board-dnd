namespace QuestBoard.Domain.Enums;

/// <summary>
/// The state of a board move: what a preview offers, or how an attempted move ended.
/// </summary>
public enum MoveStatus
{
    Ready,
    ChooseTarget,
    NotFound,
    Blocked,
    NoLinkedBoards,
    TargetNotLinked,
    DungeonMasterNotEligible,
    Changed,
    Moved,
    Failed
}
