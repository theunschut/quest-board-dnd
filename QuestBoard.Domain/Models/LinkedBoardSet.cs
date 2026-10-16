using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

/// <summary>
/// A named set of boards of one type that share a calendar view.
/// </summary>
public class LinkedBoardSet
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public BoardType BoardType { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<LinkedBoardSetBoard> Boards { get; set; } = [];

    /// <summary>
    /// A set only links anything once it holds at least two boards; a set with fewer stays
    /// stored but has no effect.
    /// </summary>
    public bool IsActive => Boards.Count >= 2;
}
