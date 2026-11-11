namespace QuestBoard.Domain.Models.BoardDeletion;

/// <summary>
/// A follow-up quest on another board that stays in place when a board is deleted and only loses
/// its link to the original quest.
/// </summary>
public sealed class BoardUnlinkedFollowUp
{
    /// <summary>The follow-up quest's title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The name of the board the follow-up quest lives on.</summary>
    public string BoardName { get; init; } = string.Empty;
}
