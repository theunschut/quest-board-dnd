using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

/// <summary>
/// A board as seen by the linked board set screens: its type and the set it is in, if any.
/// </summary>
public class LinkedBoardCandidate
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public BoardType BoardType { get; set; }

    public int? LinkedBoardSetId { get; set; }
}
