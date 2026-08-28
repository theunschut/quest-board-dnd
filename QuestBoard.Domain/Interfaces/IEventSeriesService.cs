using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

// Deliberately not IBaseService<EventSeries> -- the generic add/update/remove members that
// interface would pull in would offer a second way to remove a series that bypasses the two
// deliberate delete-versus-detach outcomes below.
public interface IEventSeriesService
{
    /// <summary>
    /// Runs the same generator that later materialization uses and returns the first configured
    /// number of firing dates on or after today, with no database access. AnchorFullyInPast is
    /// true when the first window of firing slots computed from the anchor itself are all before
    /// today, which is what lets the form explain that the listed dates start from today rather
    /// than from the anchor.
    /// </summary>
    Task<(IReadOnlyList<DateOnly> Dates, bool AnchorFullyInPast)> PreviewAsync(DateOnly anchorDate, int intervalWeeks, string cycleMask, DateOnly? endDate, CancellationToken token = default);

    /// <summary>
    /// Creates the series and its first generation pass of occurrences in one transaction, so a
    /// failure partway through leaves nothing behind. Only slots dated today or later are
    /// materialized; earlier slots are still counted for numbering but never created.
    /// </summary>
    Task<EventSeries> CreateWithFirstPassAsync(EventSeries series, CancellationToken token = default);

    /// <summary>
    /// The single idempotent materializer both the controller path and the nightly job path call.
    /// Tops the series up to the configured runway of live upcoming occurrences and returns how
    /// many were created. Safe to call repeatedly -- a slot already present, cancelled or moved is
    /// never recreated.
    /// </summary>
    Task<int> TopUpAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Returns the series, or null if it does not exist or belongs to another board.
    /// </summary>
    Task<EventSeries?> GetSeriesAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Returns every active series (not yet ended) for the active board.
    /// </summary>
    Task<IList<EventSeries>> GetActiveSeriesForActiveGroupAsync(CancellationToken token = default);

    /// <summary>
    /// Returns every occurrence of the series, past and future, cancelled included, with no date
    /// predicate.
    /// </summary>
    Task<IList<Event>> GetOccurrencesAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Returns every active series on the board currently below the configured runway, for the
    /// calendar's horizon banner.
    /// </summary>
    Task<IList<SeriesRunwayStatus>> GetSeriesBelowRunwayAsync(CancellationToken token = default);

    /// <summary>
    /// Returns how many of the series' occurrences fall before and on/after today, and how many
    /// carry a real answer rather than an automatic pass. Backs the series removal confirm.
    /// </summary>
    Task<SeriesRemovalImpact> GetRemovalImpactAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Counts live (non-cancelled) siblings of the series on the given date, excluding the given
    /// event. Used to notice a DM when moving an occurrence lands it on a date another live
    /// session of the same series already holds.
    /// </summary>
    Task<int> CountLiveSiblingsOnDateAsync(int seriesId, DateOnly date, int excludeEventId, CancellationToken token = default);

    /// <summary>
    /// Sets the series' end date. No slot fires past it. When removeFutureOccurrences is true,
    /// occurrences dated after the end date are also removed; occurrences on or before it are
    /// always kept because they record sessions that happened. Returns the number removed.
    /// </summary>
    Task<int> EndAsync(int seriesId, DateOnly endDate, bool removeFutureOccurrences, CancellationToken token = default);

    /// <summary>
    /// Removes the series and every one of its occurrences. Not a cascade -- both this and
    /// DetachAsync are deliberate, distinct outcomes.
    /// </summary>
    Task DeleteAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Drops only the recurrence rule: clears the series id and slot index on every occurrence,
    /// turning them into ordinary one-off events, then removes the series row.
    /// </summary>
    Task DetachAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// The "this and future events" edit scope. Updates the series template and then only the
    /// future occurrences that nobody has separately moved, edited or cancelled. No past
    /// occurrence is ever rewritten by any scope. Returns the number of occurrences swept.
    /// </summary>
    Task<int> ApplyTemplateToFutureAsync(int seriesId, int editedEventId, string title, string? description, TimeOnly? startTime, CancellationToken token = default);
}
