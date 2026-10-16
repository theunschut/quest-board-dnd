using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

/// <summary>
/// The other boards that share a calendar with the active board, as seen by one viewer.
/// </summary>
public class ViewerLinkedBoards
{
    public BoardType SetBoardType { get; set; }

    public IReadOnlyList<LinkedBoard> Boards { get; set; } = [];

    public static ViewerLinkedBoards Empty => new();
}
