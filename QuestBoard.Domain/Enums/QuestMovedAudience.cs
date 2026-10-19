namespace QuestBoard.Domain.Enums;

/// <summary>
/// Who a quest-moved email is addressed to, which decides its wording and whether it offers a link.
/// </summary>
public enum QuestMovedAudience
{
    /// <summary>A player whose signup came along to the target board.</summary>
    Player,

    /// <summary>The quest's Dungeon Master, who stays in charge after the move.</summary>
    DungeonMaster,

    /// <summary>A player who is not a member of the target board and whose signup was removed.</summary>
    Removed
}
