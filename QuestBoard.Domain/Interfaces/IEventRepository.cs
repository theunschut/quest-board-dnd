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
}
