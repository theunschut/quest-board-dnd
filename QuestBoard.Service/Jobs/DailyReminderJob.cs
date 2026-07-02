using QuestBoard.Domain.Interfaces;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace QuestBoard.Service.Jobs;

public class DailyReminderJob(
    IServiceScopeFactory scopeFactory,
    IBackgroundJobClient backgroundJobClient,
    ILogger<DailyReminderJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // FinalizedDate is stored as server local time (no UTC annotation on QuestEntity).
        // DateTime.Today is server local time on the LXC container (CET/CEST).
        // Comparison is correct — no timezone conversion needed.
        var tomorrow = DateTime.Today.AddDays(1);

        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
        {
            var questRepository = sp.GetRequiredService<IQuestRepository>();

            var quests = await questRepository.GetQuestsForTomorrowAllGroupsAsync(tomorrow, cancellationToken);

            if (quests.Count == 0)
            {
                logger.LogInformation(
                    "DailyReminderJob: no finalized quests found for {Date}.",
                    tomorrow.ToShortDateString());
                return;
            }

            foreach (var quest in quests)
            {
                backgroundJobClient.Enqueue<SessionReminderJob>(
                    job => job.ExecuteAsync(quest.Id, quest.GroupId, false, false, CancellationToken.None));

                logger.LogInformation(
                    "DailyReminderJob: queued SessionReminderJob for quest {QuestId} on {Date}.",
                    quest.Id, tomorrow.ToShortDateString());
            }
        });
    }
}
