using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Services;

/// <summary>
/// No-op implementation of IQuestEmailDispatcher used in test environments
/// where Hangfire is not registered (IBackgroundJobClient is unavailable).
/// </summary>
public class NullQuestEmailDispatcher : IQuestEmailDispatcher
{
    /// <inheritdoc/>
    public void EnqueueFinalizedEmail(
        int questId,
        int groupId,
        DateTime finalizedDate,
        string[] recipientEmails,
        string[] playerNames,
        string questTitle,
        string dmName,
        string questDescription,
        int challengeRating)
    {
        // No-op — Hangfire not available in Testing environment
    }

    /// <inheritdoc/>
    public void EnqueueDateChangedEmail(
        int questId,
        string[] recipientEmails,
        string[] playerNames,
        string questTitle,
        string dmName,
        DateTime oldDate,
        DateTime newDate)
    {
        // No-op — Hangfire not available in Testing environment
    }
}
