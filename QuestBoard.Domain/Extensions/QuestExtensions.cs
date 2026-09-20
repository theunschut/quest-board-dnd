using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Extensions;

public static class QuestExtensions
{
    /// <summary>
    /// True once a finalized quest's game night is a full day or more in the past, measured
    /// against the board's own today.
    /// </summary>
    /// <remarks>
    /// FinalizedDate is a floating wall-clock value in the board's own zone -- it carries no
    /// UTC instant -- so the only date it can be compared against is the board's own today.
    /// Comparing it against a UTC instant instead misclassifies a quest for the hours either
    /// side of board-local midnight, and for a board whose zone sits far from UTC it can be a
    /// whole day out. Every surface that decides "Done" versus "Finalized", and the quest log's
    /// own membership rule, share this one definition so they cannot drift apart again.
    /// </remarks>
    /// <param name="quest">
    /// Nullable so the views that hold an optional quest can call this without a null dance --
    /// a quest that is not there has no game night to have passed.
    /// </param>
    public static bool HasFinalizedGameNightPassed(this Quest? quest, DateOnly boardToday) =>
        quest is { IsFinalized: true, FinalizedDate: { } finalizedDate }
        && DateOnly.FromDateTime(finalizedDate) <= boardToday.AddDays(-1);
}
