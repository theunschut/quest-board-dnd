using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Jobs;
using Hangfire;

namespace QuestBoard.Service.Services;

/// <summary>
/// Implements IQuestEmailDispatcher by enqueueing Hangfire fire-and-forget jobs.
/// Lives in Service to avoid a Domain → Service circular dependency.
/// </summary>
public class HangfireQuestEmailDispatcher(IBackgroundJobClient jobClient) : IQuestEmailDispatcher
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
        jobClient.Enqueue<QuestFinalizedEmailJob>(j => j.ExecuteAsync(
            questId, groupId, finalizedDate, recipientEmails, playerNames,
            questTitle, dmName, questDescription, challengeRating,
            CancellationToken.None));
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
        jobClient.Enqueue<QuestDateChangedEmailJob>(j => j.ExecuteAsync(
            questId, recipientEmails, playerNames,
            questTitle, dmName, oldDate, newDate,
            CancellationToken.None));
    }
}
