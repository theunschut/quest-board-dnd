using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.BoardDeletion;

/// <summary>
/// One kind of board content a delete would remove, with how many rows of it there are.
/// </summary>
public sealed class BoardDeletionItem
{
    /// <summary>The kind of content.</summary>
    public BoardContentKind Kind { get; init; }

    /// <summary>How many rows of this kind the delete would remove.</summary>
    public int Count { get; init; }

    /// <summary>
    /// The title or name of every row, sorted A to Z ignoring case with duplicates kept. Null for
    /// kinds that are only counted, because their rows have no name worth showing.
    /// </summary>
    public IReadOnlyList<string>? Names { get; init; }
}
