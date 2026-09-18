using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class CalendarSubscriptionRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<CalendarSubscription, CalendarSubscriptionEntity>(dbContext, mapper), ICalendarSubscriptionRepository
{
    /// <inheritdoc/>
    public async Task<CalendarSubscription?> GetByTokenAsync(string feedToken, CancellationToken token = default)
    {
        // A revoked row is returned, not filtered out -- the caller needs to tell a revoked
        // address apart from one that never existed.
        var entity = await DbContext.CalendarSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.Token == feedToken, token);

        return entity == null ? null : Mapper.Map<CalendarSubscription>(entity);
    }

    /// <inheritdoc/>
    public async Task<CalendarSubscription> MintAsync(int userId, string name, string feedToken, CancellationToken token = default)
    {
        var entity = new CalendarSubscriptionEntity
        {
            UserId = userId,
            Name = name,
            Token = feedToken,
            CreatedAt = DateTime.UtcNow,
            LastFetchedAt = null,
            RevokedAt = null
        };

        await DbContext.CalendarSubscriptions.AddAsync(entity, token);
        await DbContext.SaveChangesAsync(token);

        return Mapper.Map<CalendarSubscription>(entity);
    }

    /// <inheritdoc/>
    public async Task<IList<CalendarSubscription>> GetForUserAsync(int userId, CancellationToken token = default)
    {
        var entities = await DbContext.CalendarSubscriptions
            .Where(cs => cs.UserId == userId && cs.RevokedAt == null)
            .OrderBy(cs => cs.CreatedAt)
            .AsNoTracking()
            .ToListAsync(token);

        return Mapper.Map<IList<CalendarSubscription>>(entities);
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(int id, int userId, string name, CancellationToken token = default)
    {
        // Matched on Id and UserId together so a caller can never rename someone else's
        // subscription by guessing or supplying another member's row id.
        var entity = await DbSet.FirstOrDefaultAsync(cs => cs.Id == id && cs.UserId == userId, token);
        if (entity == null) return false;

        entity.Name = name;
        await DbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(int id, int userId, DateTime revokedAt, CancellationToken token = default)
    {
        var entity = await DbSet.FirstOrDefaultAsync(cs => cs.Id == id && cs.UserId == userId, token);
        if (entity == null) return false;

        entity.RevokedAt = revokedAt;
        await DbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task TouchLastFetchedAsync(int id, DateTime fetchedAt, TimeSpan minimumInterval, CancellationToken token = default)
    {
        // This is the only write a read-only endpoint performs, and a calendar client polls on
        // its own schedule which the server does not control, so the timestamp write is
        // throttled by time rather than performed on every poll. When the guard below rejects,
        // this method returns without calling SaveChangesAsync at all -- the point is that a
        // hammered address performs no write, not a no-op write.
        var entity = await DbSet.FirstOrDefaultAsync(cs => cs.Id == id, token);
        if (entity == null) return;

        if (entity.LastFetchedAt == null || fetchedAt - entity.LastFetchedAt.Value >= minimumInterval)
        {
            entity.LastFetchedAt = fetchedAt;
            await DbContext.SaveChangesAsync(token);
        }
    }
}
