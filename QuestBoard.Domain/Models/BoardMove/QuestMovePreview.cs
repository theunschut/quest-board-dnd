using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// A quest move preview or outcome, with the quest snapshot and the plan it would apply.
/// </summary>
public class QuestMovePreview : MovePreview
{
    public Quest? Quest { get; set; }

    public string DmName { get; set; } = string.Empty;

    public int DungeonMasterId { get; set; }

    public int CharactersCleared { get; set; }

    public int SeatsOpened { get; set; }

    public int PromotedCount { get; set; }

    public string? OriginalQuestTitle { get; set; }

    public string? FollowUpQuestTitle { get; set; }

    public QuestMovePlan? Plan { get; set; }
}
