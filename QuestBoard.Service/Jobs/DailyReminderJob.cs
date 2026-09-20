using QuestBoard.Domain.Interfaces;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace QuestBoard.Service.Jobs;

public class DailyReminderJob(
    IServiceScopeFactory scopeFactory,
    IBackgroundJobClient backgroundJobClient,
    IBoardClock boardClock,
    ILogger<DailyReminderJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // FinalizedDate is a naive wall-clock value the DM typed, with no zone attached, so
        // "tomorrow" is computed on the board's configured clock rather than the container's --
        // keeping this comparison correct through the hours either side of midnight.
        var tomorrow = boardClock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue);

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
