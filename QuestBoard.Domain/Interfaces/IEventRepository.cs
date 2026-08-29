using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IEventRepository : IBaseRepository<Event>
{
    /// <summary>
    /// Returns every event in the active group, ordered by date then start time. Group scoping
    /// is enforced by the entity's query filter, not by a parameter on this method. This
    /// deliberately fetches all events rather than a date range, matching how the quest calendar
    /// read already works, with month filtering done in the view model.
    /// </summary>
    Task<IList<Event>> GetEventsForCalendarAsync(CancellationToken token = default);

    /// <summary>
    /// Returns the event, or null if it does not exist or belongs to another board.
    /// </summary>
    Task<Event?> GetEventWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the group that owns the given repeating-schedule row, or null if that row does
    /// not exist or is not visible to the active board. Callers use the returned value to reject
    /// a write that would attach an event to another board's schedule, and a null result must be
    /// treated as a rejection rather than as "no constraint".
    /// </summary>
    Task<int?> GetSeriesGroupIdAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Inserts the event together with one automatic signup per member id in a single save, so
    /// a campaign board can never hold an event that some members have no row for. The
    /// automatic rows deliberately leave the answered marker unset.
    /// </summary>
    Task AddWithCampaignFanOutAsync(Event newEvent, IEnumerable<int> memberIds, CancellationToken token = default);

    /// <summary>
    /// Sets or clears the cancelled marker on a single event by id. A null argument un-cancels.
    /// Returns false when the event does not exist or belongs to another board. This is a
    /// narrow scalar write rather than a mapped update, because the event's loaded signup
    /// collection would otherwise be dropped by a generic update.
    /// </summary>
    Task<bool> SetCancelledAsync(int eventId, DateTime? cancelledAt, CancellationToken token = default);

    /// <summary>
    /// Overwrites the title, description and start time on every event whose id is in the
    /// given collection, in a single save, and returns how many rows were actually updated.
    /// Deciding which occurrences are eligible for the sweep happens elsewhere; this method
    /// only performs the write. Returns 0 immediately without touching the database when the
    /// collection is empty.
    /// </summary>
    Task<int> ApplyTemplateToOccurrencesAsync(IReadOnlyCollection<int> eventIds, string title, string? description, TimeOnly? startTime, CancellationToken token = default);

    /// <summary>
    /// Counts events belonging to the given series, dated on the given date, that are not
    /// cancelled and are not the excluded event itself. Used to notice a DM when a moved
    /// occurrence lands on a date another live session of the same series already holds; a
    /// cancelled sibling never counts.
    /// </summary>
    Task<int> CountLiveSiblingsOnDateAsync(int seriesId, DateOnly date, int excludeEventId, CancellationToken token = default);

    /// <summary>
    /// Returns every event belonging to the given series, past and future, cancelled included,
    /// ordered by date then start time, with no date predicate.
    /// </summary>
    Task<IList<Event>> GetOccurrencesForSeriesAsync(int seriesId, CancellationToken token = default);

    /// <summary>
    /// Returns the next <paramref name="take"/> live events dated on or after
    /// <paramref name="today"/>, each paired with every signup row and the signing member's
    /// name, in a single round trip. Group scoping is enforced by the entity's query filter,
    /// not by a parameter on this method. A cancelled occurrence is excluded even though its
    /// signup rows still exist, and the lower bound is date-only, so an event keeps its place
    /// for the whole of today regardless of its start time and an all-day event with a null
    /// start time is never dropped.
    /// </summary>
    Task<IList<EventWithSignups>> GetUpcomingWithSignupsAsync(DateOnly today, int take, CancellationToken token = default);
}
