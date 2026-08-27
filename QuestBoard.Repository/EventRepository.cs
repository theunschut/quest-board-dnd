using AutoMapper;
using QuestBoard.Domain.Enums;
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

    /// <inheritdoc/>
    public async Task AddWithCampaignFanOutAsync(Event newEvent, IEnumerable<int> memberIds, CancellationToken token = default)
    {
        var entity = Mapper.Map<EventEntity>(newEvent);

        // Distinct so a duplicated id in the caller's member list can never violate the
        // signup table's unique (EventId, UserId) pair index. Each automatic row leaves
        // Availability at Yes and its answered-marker column at its default (unset) -- that
        // default is what keeps an automatic row distinguishable from a real answer later.
        foreach (var memberId in memberIds.Distinct())
        {
            entity.Signups.Add(new EventSignupEntity
            {
                UserId = memberId,
                Availability = (int)VoteType.Yes
            });
        }

        // One save for the whole graph -- the relationship supplies EventId to every signup
        // row when the event itself is saved, so there is never a moment where the event
        // exists without every member's row.
        await DbSet.AddAsync(entity, token);
        await DbContext.SaveChangesAsync(token);
        newEvent.Id = entity.Id;
    }
}
