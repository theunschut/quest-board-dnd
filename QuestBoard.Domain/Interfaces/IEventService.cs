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

    /// <summary>
    /// Builds the availability overview for the next <paramref name="take"/> live upcoming
    /// events in a single repository round trip: a member axis built from the union of members
    /// holding a signup row (never a membership query), a five-state cell per member per event,
    /// three per-row counts, and whether more events exist beyond the requested window. The
    /// answered marker on each signup row is the only input to the confirmed/unconfirmed
    /// distinction.
    /// </summary>
    Task<EventAvailabilityOverview> GetAvailabilityOverviewAsync(int take, CancellationToken token = default);

    /// <summary>
    /// Builds the next <paramref name="take"/> upcoming events across the boards named in
    /// <paramref name="memberGroupIds"/> into a single chronologically ordered agenda, each row
    /// carrying the viewer's own cell and the event's complete roster. Scoping comes entirely
    /// from <paramref name="memberGroupIds"/>, which the caller reads fresh per request from the
    /// viewer's own memberships -- an empty set is a legitimate input yielding an empty agenda,
    /// not a short-circuited call. Every fetched row is re-checked against
    /// <paramref name="memberGroupIds"/> before the window is trimmed, and any row outside that
    /// set is dropped rather than surfaced -- and logged at Error, because a surviving foreign
    /// row can only mean the query's own board predicate was lost, which an operator has to hear
    /// about even though the reader is already protected by the drop.
    /// </summary>
    Task<CrossBoardAgenda> GetCrossBoardAgendaAsync(IReadOnlyCollection<int> memberGroupIds, int currentUserId, int take, CancellationToken token = default);
}
