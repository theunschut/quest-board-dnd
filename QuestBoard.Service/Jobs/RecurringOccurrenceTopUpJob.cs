using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace QuestBoard.Service.Jobs;

public class RecurringOccurrenceTopUpJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringOccurrenceTopUpJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IList<GroupWithMemberCount> boards = [];

        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
        {
            var groupRepository = sp.GetRequiredService<IGroupRepository>();
            boards = await groupRepository.GetAllWithMemberCountAsync(cancellationToken);
        });

        var boardsProcessed = 0;
        var seriesProcessed = 0;
        var occurrencesCreated = 0;
        var boardsFailed = 0;

        foreach (var board in boards)
        {
            try
            {
                // Every board gets its own scope and its own real, non-null group id before any
                // repository call runs inside it. This job writes across every board, so it must
                // never reach for a cross-board filter bypass the way a read-only sweep could --
                // the per-board scope is what keeps each write inside that board's own data.
                await HangfireJobHelper.RunInScopeAsync(scopeFactory, board.Id, async sp =>
                {
                    var seriesService = sp.GetRequiredService<IEventSeriesService>();

                    var activeSeries = await seriesService.GetActiveSeriesForActiveGroupAsync(cancellationToken);

                    foreach (var series in activeSeries)
                    {
                        var created = await seriesService.TopUpAsync(series.Id, cancellationToken);
                        occurrencesCreated += created;
                        seriesProcessed++;
                    }
                });

                boardsProcessed++;
            }
            catch (Exception ex)
            {
                boardsFailed++;
                logger.LogError(
                    ex,
                    "RecurringOccurrenceTopUpJob: top-up failed for board {BoardId}.",
                    board.Id);
            }
        }

        logger.LogInformation(
            "RecurringOccurrenceTopUpJob: boards processed {BoardsProcessed}, series processed {SeriesProcessed}, occurrences created {OccurrencesCreated}, boards failed {BoardsFailed}.",
            boardsProcessed, seriesProcessed, occurrencesCreated, boardsFailed);

        if (boardsFailed > 0)
        {
            // The sweep still reports failure so the retry policy applies. A retry is safe because
            // every write above goes through the slot-keyed idempotent materializer -- re-running
            // only creates what is still missing, never a duplicate.
            throw new InvalidOperationException(
                $"RecurringOccurrenceTopUpJob: {boardsFailed} of {boards.Count} board(s) failed to top up.");
        }
    }
}
