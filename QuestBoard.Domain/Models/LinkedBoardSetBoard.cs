namespace QuestBoard.Domain.Models;

/// <summary>
/// A board that belongs to a linked board set.
/// </summary>
public class LinkedBoardSetBoard
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
