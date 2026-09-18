using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class EventSignupRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<EventSignup, EventSignupEntity>(dbContext, mapper), IEventSignupRepository
{
    /// <inheritdoc/>
    public async Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default)
    {
        // The ambient query filter scopes reads only, so an insert has to re-ask whether the
        // event belongs to the caller's board before it can write anything against it.
        var eventExists = await DbContext.Events.AnyAsync(e => e.Id == eventId, token);
        if (!eventExists)
        {
            throw new ArgumentException("Event not found", nameof(eventId));
        }

        var entity = await DbSet.FirstOrDefaultAsync(es => es.EventId == eventId && es.UserId == userId, token);

        if (entity != null)
        {
            entity.Availability = (int)availability;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // The creating write stamps the answered timestamp too, so the stamp means "a
            // person set this" uniformly regardless of whether this is the first click or a
            // later change.
            await DbContext.EventSignups.AddAsync(new EventSignupEntity
            {
                EventId = eventId,
                UserId = userId,
                Availability = (int)availability,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, token);
        }

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> WithdrawAsync(int eventId, int userId, CancellationToken token = default)
    {
        var entity = await DbSet.FirstOrDefaultAsync(es => es.EventId == eventId && es.UserId == userId, token);
        if (entity == null) return false;

        DbSet.Remove(entity);
        await DbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default)
    {
        // The ambient query filter is the correct scoping here: this only ever runs from the
        // event details request, where the event itself was already fetched through the same
        // filter. Roster ordering is alphabetical by member name, so the view does not need to
        // re-sort.
        var entities = await DbContext.EventSignups
            .Include(es => es.User)
            .Where(es => es.EventId == eventId)
            .OrderBy(es => es.User.Name)
            .ToListAsync(token);

        return Mapper.Map<IList<EventSignup>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<EventFeedRow>> GetFeedRowsForUserAsync(
        int userId,
        IReadOnlyCollection<int> memberGroupIds,
        DateOnly windowStart,
        DateOnly windowEnd,
        CancellationToken token = default)
    {
        // Rooted at EventSignups rather than Events -- an event the caller holds no signup row
        // on can never appear here, which is what a calendar feed needs and what no existing
        // cross-board event query provides. Scope is re-imposed immediately by memberGroupIds,
        // supplied by the caller from a fresh membership read taken this same request -- this
        // bypass is therefore strictly narrower than the ambient filter for any single board,
        // never broader. No roster ever reaches an entry, so there is deliberately no
        // Include(Signups).ThenInclude(User) here.
        var entities = await DbContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == userId
                && memberGroupIds.Contains(es.Event.GroupId)
                && es.Event.CancelledAt == null
                && es.Event.Date >= windowStart && es.Event.Date <= windowEnd)
            .OrderBy(es => es.Event.Date)
                .ThenBy(es => es.Event.StartTime)
                .ThenBy(es => es.Event.Id)
            .Include(es => es.Event)
            .AsNoTracking()
            .ToListAsync(token);

        return entities
            .Select(entity => new EventFeedRow
            {
                Event = Mapper.Map<Event>(entity.Event),
                Availability = (VoteType)entity.Availability
            })
            .ToList();
    }
}
