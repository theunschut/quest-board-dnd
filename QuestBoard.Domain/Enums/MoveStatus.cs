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
    Failed,

    /// <summary>
    /// The target board does not use this kind of entry: its page for quests or events is switched
    /// off, so moving the entry there would leave it pointing into a page that board refuses.
    /// </summary>
    TargetPageOff
}
