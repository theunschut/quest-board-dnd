using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IEventService : IBaseService<Event>
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
    /// Returns false when the event does not exist or belongs to another board.
    /// </summary>
    Task<bool> SetCancelledAsync(int eventId, DateTime? cancelledAt, CancellationToken token = default);
}
