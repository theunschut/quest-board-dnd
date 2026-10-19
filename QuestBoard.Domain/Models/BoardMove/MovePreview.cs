using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// What a move would do, or what it did: the shared shape for quest and event moves. The
/// <see cref="Status"/> doubles as the outcome of an attempted move.
/// </summary>
public class MovePreview
{
    public MoveStatus Status { get; set; }

    public MoveBlockReason BlockReason { get; set; }

    public int EntryId { get; set; }

    public string EntryTitle { get; set; } = string.Empty;

    public int SourceGroupId { get; set; }

    public string SourceBoardName { get; set; } = string.Empty;

    public IReadOnlyList<LinkedBoard> LinkedBoards { get; set; } = [];

    public LinkedBoard? Target { get; set; }

    public BoardType TargetBoardType { get; set; }

    public int KeptCount { get; set; }

    public IReadOnlyList<MovePerson> Dropped { get; set; } = [];

    public string Stamp { get; set; } = string.Empty;
}
