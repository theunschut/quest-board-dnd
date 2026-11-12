namespace QuestBoard.Domain.Models.BoardDeletion;

/// <summary>
/// What deleting a board would remove, kind by kind, and which rows on other boards would only be
/// unlinked from it.
/// </summary>
public sealed class BoardDeletionPreview
{
    /// <summary>
    /// Every kind of board content in the order the confirmation page lists them, including kinds
    /// with no rows (count zero).
    /// </summary>
    public IReadOnlyList<BoardDeletionItem> Items { get; init; } = [];

    /// <summary>Follow-up quests on other boards that would lose their link to an original quest.</summary>
    public IReadOnlyList<BoardUnlinkedFollowUp> FollowUpsOnOtherBoards { get; init; } = [];

    /// <summary>How many signups on other boards would lose the character they signed up with.</summary>
    public int SignupsLosingCharacter { get; init; }

    /// <summary>How many events on other boards would leave a repeating series.</summary>
    public int EventsLeavingSeries { get; init; }
}
