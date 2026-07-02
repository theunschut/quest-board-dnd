using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Jobs;
using Hangfire;

namespace QuestBoard.Service.Services;

/// <summary>
/// Implements IReminderJobDispatcher by enqueueing a Hangfire fire-and-forget job.
/// Lives in Service to avoid a Domain → Service circular dependency.
/// </summary>
public class HangfireReminderJobDispatcher(IBackgroundJobClient jobClient) : IReminderJobDispatcher
{
    /// <inheritdoc/>
    public void EnqueueSessionReminder(int questId, int groupId, bool forceResend = false, bool useYesMaybeVoters = false)
    {
        jobClient.Enqueue<SessionReminderJob>(j => j.ExecuteAsync(questId, groupId, forceResend, useYesMaybeVoters, CancellationToken.None));
    }
}
