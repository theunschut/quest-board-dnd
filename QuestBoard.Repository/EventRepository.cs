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

    /// <inheritdoc/>
    public async Task<bool> SetCancelledAsync(int eventId, DateTime? cancelledAt, CancellationToken token = default)
    {
        // A narrow scalar write on purpose: EventEntity carries a Signups navigation
        // collection, and mapping a domain model over this tracked entity through the
        // generic update path would drop that collection.
        var entity = await DbSet.FirstOrDefaultAsync(e => e.Id == eventId, token);
        if (entity == null) return false;

        entity.CancelledAt = cancelledAt;
        await DbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> ApplyTemplateToOccurrencesAsync(IReadOnlyCollection<int> eventIds, string title, string? description, TimeOnly? startTime, CancellationToken token = default)
    {
        if (eventIds.Count == 0) return 0;

        var entities = await DbSet.Where(e => eventIds.Contains(e.Id)).ToListAsync(token);
        foreach (var entity in entities)
        {
            entity.Title = title;
            entity.Description = description;
            entity.StartTime = startTime;
        }

        await DbContext.SaveChangesAsync(token);
        return entities.Count;
    }

    /// <inheritdoc/>
    public async Task<int> CountLiveSiblingsOnDateAsync(int seriesId, DateOnly date, int excludeEventId, CancellationToken token = default)
    {
        // A cancelled sibling deliberately does not count -- this backs a notice, not a
        // block, so a double session on the same date is legitimate and must not be hidden.
        return await DbSet.CountAsync(e =>
            e.SeriesId == seriesId &&
            e.Date == date &&
            e.Id != excludeEventId &&
            e.CancelledAt == null, token);
    }

    /// <inheritdoc/>
    public async Task<IList<Event>> GetOccurrencesForSeriesAsync(int seriesId, CancellationToken token = default)
    {
        // No date predicate -- this backs both the series page's occurrence table and the
        // eligibility decision for the template sweep, which needs every occurrence to work
        // out what it can safely touch.
        var entities = await DbSet
            .Where(e => e.SeriesId == seriesId)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartTime)
            .ToListAsync(token);

        return Mapper.Map<IList<Event>>(entities);
    }
}
