using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

// A retired subscription keeps answering "gone" so that a calendar client still holding the
// address learns to stop asking. Once it has been retired long enough that any such client has
// given up, the row is removed so the table does not grow for the lifetime of the application.
// The visible consequence: after the purge, the same address answers "not found" instead of
// "gone" -- a future reader should not mistake that for a bug.
public class CalendarSubscriptionRetentionJob(
    IServiceScopeFactory scopeFactory,
    ILogger<CalendarSubscriptionRetentionJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var purged = 0;
        var cutoff = DateTime.MinValue;

        // A single scope, not one per board -- this table is deliberately not group-filtered,
        // so the sweep needs no group context at all.
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
        {
            var options = sp.GetRequiredService<IOptions<CalendarFeedOptions>>().Value;
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var repository = sp.GetRequiredService<ICalendarSubscriptionRepository>();

            cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.RetentionDays);
            purged = await repository.PurgeRetiredBeforeAsync(cutoff, cancellationToken);
        });

        // A count and a cutoff are all an operator needs -- this job has no reason to name an
        // individual subscription, so no subscription id and no address ever appear here.
        logger.LogInformation(
            "CalendarSubscriptionRetentionJob: purged {PurgedCount} subscription(s) retired before {Cutoff}.",
            purged, cutoff);
    }
}
