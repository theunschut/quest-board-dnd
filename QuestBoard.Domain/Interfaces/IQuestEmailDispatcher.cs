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
}
