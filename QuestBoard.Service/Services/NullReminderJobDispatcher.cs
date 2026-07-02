using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Services;

/// <summary>
/// No-op implementation of IReminderJobDispatcher used in test environments
/// where Hangfire is not registered (IBackgroundJobClient is unavailable).
/// </summary>
public class NullReminderJobDispatcher : IReminderJobDispatcher
{
    /// <inheritdoc/>
    public void EnqueueSessionReminder(int questId, int groupId, bool forceResend = false, bool useYesMaybeVoters = false)
    {
        // No-op — Hangfire not available in Testing environment
    }
}
