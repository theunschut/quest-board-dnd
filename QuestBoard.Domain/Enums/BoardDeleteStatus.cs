namespace QuestBoard.Domain.Enums;

/// <summary>
/// How an attempt to delete a board ended.
/// </summary>
public enum BoardDeleteStatus
{
    /// <summary>The board and everything scoped to it was removed.</summary>
    Deleted,

    /// <summary>No board with that id exists, so there was nothing to delete.</summary>
    NotFound,

    /// <summary>The board still has members, so nothing was deleted.</summary>
    HasMembers,

    /// <summary>The typed name did not match the board's name exactly, so nothing was deleted.</summary>
    NameMismatch
}
