using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Dispatches quest-related email jobs to the background job infrastructure.
/// Defined in Domain so QuestService can call it without taking a dependency on Service-layer types.
/// </summary>
public interface IQuestEmailDispatcher
{
    /// <summary>
    /// Enqueues a background job to email the selected players that the quest has been finalized.
    /// </summary>
    void EnqueueFinalizedEmail(
        int questId,
        int groupId,
        DateTime finalizedDate,
        string[] recipientEmails,
        string[] playerNames,
        string questTitle,
        string dmName,
        string questDescription,
        int challengeRating);

    /// <summary>
    /// Enqueues a background job to email affected players that the quest's proposed date changed.
    /// </summary>
    void EnqueueDateChangedEmail(
        int questId,
        string[] recipientEmails,
        string[] playerNames,
        string questTitle,
        string dmName,
        DateTime oldDate,
        DateTime newDate);

    /// <summary>
    /// Enqueues a background job to email the single player who was auto-promoted off the
    /// waitlist into a freed seat. Deliberately takes a singular recipient/name pair (not
    /// arrays) so this method can never broadcast to more than one player.
    /// </summary>
    void EnqueueWaitlistPromotedEmail(
        int questId,
        int groupId,
        DateTime finalizedDate,
        string recipientEmail,
        string playerName,
        string questTitle,
        string dmName,
        string questDescription,
        int challengeRating);

    /// <summary>
    /// Enqueues one background job to email a single recipient that a quest moved to another board.
    /// Takes a singular recipient so a retried job can never resend to anyone else, and runs under
    /// the given board id, which the caller sets to the board the quest moved to.
    /// <paramref name="sessionDate"/> is a wall-clock value and is passed through unconverted.
    /// </summary>
    void EnqueueQuestMovedEmail(
        int questId,
        int groupId,
        QuestMovedAudience audience,
        string recipientEmail,
        string questTitle,
        string dmName,
        string boardName,
        DateTime? sessionDate);

    /// <summary>
    /// Enqueues one background job to email the single player promoted off the waitlist because the
    /// quest moved boards, using the waitlist-promoted mail with a note naming the new board.
    /// Takes a singular recipient so this method can never broadcast, and runs under the given
    /// board id, which the caller sets to the board the quest moved to.
    /// </summary>
    void EnqueueWaitlistPromotedAfterMoveEmail(
        int questId,
        int groupId,
        DateTime finalizedDate,
        string recipientEmail,
        string playerName,
        string questTitle,
        string dmName,
        string questDescription,
        int challengeRating,
        string movedToBoardName);
}
