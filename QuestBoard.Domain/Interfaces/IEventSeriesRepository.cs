using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IEventSeriesRepository : IBaseRepository<EventSeries>
{
    /// <summary>
    /// Returns the series, or null if it does not exist or belongs to another board.
    /// </summary>
    Task<EventSeries?> GetSeriesAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Returns every series in the active board whose end date is null or on/after the given
    /// date, ordered by id. An ended series generates nothing further.
    /// </summary>
    Task<IList<EventSeries>> GetActiveSeriesAsync(DateOnly today, CancellationToken token = default);

    /// <summary>
    /// Returns every slot index the series has ever produced, cancelled occurrences included,
    /// with no date predicate. This is the single source of the idempotency answer for
    /// cancelled, moved and edited occurrences alike; restricting it to a window would let an
    /// occurrence moved far outside that window read as free and regenerate on its original
    /// date, leaving two rows. This signature must never grow a date parameter.
    /// </summary>
    Task<IReadOnlyCollection<int>> GetSlotIndexesForSeriesAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Counts live (non-cancelled) occurrences of the series dated today or later. This is the
    /// runway measure: live upcoming sessions rather than a date horizon, so a fortnightly
    /// series and a weekly series each get the same number of upcoming sessions.
    /// </summary>
    Task<int> CountLiveFutureOccurrencesAsync(int seriesId, DateOnly today, CancellationToken token = default);

    /// <summary>
    /// Returns every active series below the given live-future-occurrence target, with its id,
    /// title and current count, ordered by count ascending. Backs the DM-visible runway
    /// horizon banner.
    /// </summary>
    Task<IList<SeriesRunwayStatus>> GetSeriesBelowRunwayAsync(DateOnly today, int runwayTarget, CancellationToken token = default);

    /// <summary>
    /// Returns how many of the series' occurrences fall before and on/after the given date, and
    /// how many signup rows on those occurrences carry a real answer (not an automatic pass).
    /// Backs the series removal confirm.
    /// </summary>
    Task<SeriesRemovalImpact> GetRemovalImpactAsync(int seriesId, DateOnly today, CancellationToken token = default);

    /// <summary>
    /// Sets the series' end date. When <paramref name="removeFutureOccurrences"/> is true, also
    /// removes occurrences of the series dated after the end date; occurrences dated on or
    /// before the end date are always kept because they record sessions that happened. Returns
    /// the number of occurrences removed.
    /// </summary>
    Task<int> SetEndDateAsync(int seriesId, DateOnly endDate, bool removeFutureOccurrences, CancellationToken token = default);

    /// <summary>
    /// Overwrites the series' template fields (title, description, start time). Returns false
    /// when the series does not exist or belongs to another board. This is the only writer of
    /// the series template.
    /// </summary>
    Task<bool> SetTemplateAsync(int seriesId, string title, string? description, TimeOnly? startTime, CancellationToken token = default);

    /// <summary>
    /// Removes every occurrence of the series and then the series row itself. Occurrence
    /// removal happens first because nothing declares a cascading delete behaviour from an
    /// occurrence back to its series, so a series row with occurrences still referencing it
    /// cannot be removed on its own.
    /// </summary>
    Task DeleteWithOccurrencesAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Clears the series id and slot index on every occurrence of the series, turning them into
    /// ordinary one-off events, and then removes the series row. A cancelled occurrence keeps
    /// its cancelled marker.
    /// </summary>
    Task DetachOccurrencesAndDeleteAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Creates the series and its first generation pass of occurrences (with campaign fan-out
    /// where applicable) as a single unit. Wraps the whole operation in a transaction when the
    /// provider supports one, so a failure partway through leaves nothing behind.
    /// </summary>
    Task CreateWithOccurrencesAsync(EventSeries series, IReadOnlyList<Event> occurrences, IReadOnlyCollection<int> campaignMemberIds, CancellationToken token = default);
}
