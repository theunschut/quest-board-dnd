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
}
