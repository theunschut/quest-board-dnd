using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class EventRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<Event, EventEntity>(dbContext, mapper), IEventRepository
{
    /// <inheritdoc/>
    public async Task<IList<Event>> GetEventsForCalendarAsync(CancellationToken token = default)
    {
        // Group scoping is enforced entirely by EventEntity's fail-closed query filter here --
        // no manual GroupId .Where is needed or added. This deliberately fetches every event
        // rather than a date range, matching the quest calendar read, because month filtering
        // happens in the view model.
        var entities = await DbContext.Events
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartTime)
            .ToListAsync(token);

        return Mapper.Map<IList<Event>>(entities);
    }

    /// <inheritdoc/>
    public async Task<Event?> GetEventWithDetailsAsync(int id, CancellationToken token = default)
    {
        // A request for an event on another board returns null here because the query filter
        // excludes it, which is what turns a cross-board identifier into a not-found response.
        var entity = await DbContext.Events.FirstOrDefaultAsync(e => e.Id == id, token);
        return entity == null ? null : Mapper.Map<Event>(entity);
    }

    /// <inheritdoc/>
    public async Task<int?> GetSeriesGroupIdAsync(int seriesId, CancellationToken token = default)
    {
        // The query filter already hides another board's schedule, so a null result here means
        // the schedule is either absent or not ours; the caller compares the returned group
        // against the active board as a second, independent check so the write is still rejected
        // if the filter is ever weakened.
        var series = await DbContext.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId, token);
        return series?.GroupId;
    }
}
