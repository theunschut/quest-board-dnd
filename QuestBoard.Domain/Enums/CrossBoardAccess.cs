namespace QuestBoard.Domain.Enums;

/// <summary>
/// What a cross-board route asks of the viewer on the board it would take them to. A board
/// where the viewer could not open the page is never a place to send them: the link then
/// behaves exactly as a link to an id that does not exist. The list is closed on purpose, so
/// every registered route has to state its rule.
/// </summary>
public enum CrossBoardAccess
{
    /// <summary>Any member of the board may open the page, as long as the board offers it.</summary>
    Member,

    /// <summary>The page needs a Dungeon Master or Admin on the board.</summary>
    DungeonMasterTier,

    /// <summary>
    /// The page needs a Dungeon Master or Admin on the board, and then the quest's own Dungeon
    /// Master or an Admin. The board narrows by role; whose quest it is belongs to the record.
    /// </summary>
    QuestOwnerOrAdmin,

    /// <summary>
    /// The page needs a member of the board, and then the character's owner or an Admin. The
    /// board narrows by page; whose character it is belongs to the record.
    /// </summary>
    CharacterOwnerOrAdmin,

    /// <summary>
    /// The quest log shows only quests that have been played or closed. The board narrows by
    /// page; whether the quest is finished belongs to the record.
    /// </summary>
    CompletedQuest,

    /// <summary>
    /// A contact is shown when it is revealed, when the viewer created it, or to a Dungeon
    /// Master or Admin. The board narrows by page; the rest belongs to the record.
    /// </summary>
    ContactVisible,

    /// <summary>
    /// A shop item is shown to members only once it is published and on sale, and always to a
    /// Dungeon Master or Admin. The board narrows by page; the rest belongs to the record.
    /// </summary>
    ShopItemVisible,

    /// <summary>
    /// Editing another member's profile needs an Admin on the board. Editing one's own profile
    /// never reaches a cross-board lookup.
    /// </summary>
    AdminForOtherUser
}
