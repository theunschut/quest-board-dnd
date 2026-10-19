namespace QuestBoard.Domain.Enums;

/// <summary>
/// Why an entry cannot be moved to another board.
/// </summary>
public enum MoveBlockReason
{
    None,
    Closed,
    Played,
    SeriesOccurrence,
    Past
}
