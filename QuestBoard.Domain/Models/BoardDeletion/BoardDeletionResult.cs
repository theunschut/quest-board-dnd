using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.BoardDeletion;

/// <summary>
/// The outcome of one attempt to delete a board: how it ended and the name of the board it
/// concerned.
/// </summary>
public sealed class BoardDeletionResult
{
    /// <summary>How the attempt ended.</summary>
    public BoardDeleteStatus Status { get; init; }

    /// <summary>The board's name, empty when no such board was found.</summary>
    public string BoardName { get; init; } = string.Empty;

    /// <summary>
    /// How many rows of each kind of board content the delete removed. Empty for an attempt that
    /// changed nothing.
    /// </summary>
    public IReadOnlyDictionary<BoardContentKind, int> RemovedCounts { get; init; }
        = new Dictionary<BoardContentKind, int>();

    /// <summary>An attempt that changed nothing, for the given reason.</summary>
    public static BoardDeletionResult Refused(BoardDeleteStatus status, string boardName = "")
        => new() { Status = status, BoardName = boardName };
}
