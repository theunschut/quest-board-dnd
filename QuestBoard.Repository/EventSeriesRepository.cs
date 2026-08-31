using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

// The slot-existence query below (GetSlotIndexesForSeriesAsync) deliberately takes no date
// parameter. Answering "has this slot already been handled?" by looking only inside a runway
// window seems like a harmless optimisation, but a DM can drag an occurrence far into the
// future or back into the past, which takes it outside any window a caller might pick. Once
// that happens, a windowed query says the slot is free and the generator recreates it on its
// original date, leaving two rows for the same session. Reading every slot the series has
// ever produced, with cancelled occurrences included, is what keeps that answer correct no
// matter where an occurrence has been moved.
internal class EventSeriesRepository(QuestBoardContext dbContext, IMapper mapper, IEventRepository eventRepository)
    : BaseRepository<EventSeries, EventSeriesEntity>(dbContext, mapper), IEventSeriesRepository
{
    /// <inheritdoc/>
    public async Task<EventSeries?> GetSeriesAsync(int seriesId, CancellationToken token = default)
    {
        var entity = await DbSet.FirstOrDefaultAsync(s => s.Id == seriesId, token);
        return entity == null ? null : Mapper.Map<EventSeries>(entity);
    }

    /// <inheritdoc/>
    public async Task<IList<EventSeries>> GetActiveSeriesAsync(DateOnly today, CancellationToken token = default)
    {
        var entities = await DbSet
            .Where(s => s.EndDate == null || s.EndDate >= today)
            .OrderBy(s => s.Id)
            .ToListAsync(token);

        return Mapper.Map<IList<EventSeries>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<int>> GetSlotIndexesForSeriesAsync(int seriesId, CancellationToken token = default)
    {
        var slots = await DbContext.Events
            .Where(e => e.SeriesId == seriesId && e.SeriesSlotIndex != null)
            .Select(e => e.SeriesSlotIndex!.Value)
            .ToListAsync(token);

        return slots.ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<int> CountLiveFutureOccurrencesAsync(int seriesId, DateOnly today, CancellationToken token = default)
    {
        return await DbContext.Events.CountAsync(e =>
            e.SeriesId == seriesId &&
            e.CancelledAt == null &&
            e.Date >= today, token);
    }

    /// <inheritdoc/>
    public async Task<IList<SeriesRunwayStatus>> GetSeriesBelowRunwayAsync(DateOnly today, int runwayTarget, CancellationToken token = default)
    {
        // A group join keeps this to one round trip instead of one count query per series.
        var query =
            from series in DbSet.Where(s => s.EndDate == null || s.EndDate >= today)
            join occurrence in DbContext.Events.Where(e => e.CancelledAt == null && e.Date >= today)
                on series.Id equals occurrence.SeriesId into liveOccurrences
            select new SeriesRunwayStatus
            {
                SeriesId = series.Id,
                Title = series.Title,
                UpcomingCount = liveOccurrences.Count()
            };

        var statuses = await query.ToListAsync(token);

        return statuses
            .Where(status => status.UpcomingCount < runwayTarget)
            .OrderBy(status => status.UpcomingCount)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SeriesRemovalImpact> GetRemovalImpactAsync(int seriesId, DateOnly today, CancellationToken token = default)
    {
        var occurrences = await DbContext.Events
            .Where(e => e.SeriesId == seriesId)
            .Select(e => new { e.Id, e.Date })
            .ToListAsync(token);

        var pastCount = occurrences.Count(o => o.Date < today);
        var futureCount = occurrences.Count - pastCount;

        var occurrenceIds = occurrences.Select(o => o.Id).ToList();
        var answeredCount = await DbContext.EventSignups
            .CountAsync(s => occurrenceIds.Contains(s.EventId) && s.UpdatedAt != null, token);

        return new SeriesRemovalImpact
        {
            PastCount = pastCount,
            FutureCount = futureCount,
            AnsweredCount = answeredCount
        };
    }

    /// <inheritdoc/>
    public async Task<int> SetEndDateAsync(int seriesId, DateOnly endDate, bool removeFutureOccurrences, CancellationToken token = default)
    {
        var entity = await DbSet.FirstOrDefaultAsync(s => s.Id == seriesId, token);
        if (entity == null) return 0;

        entity.EndDate = endDate;

        var removedCount = 0;
        if (removeFutureOccurrences)
        {
            // Occurrences dated on or before the end date are always kept -- they record
            // sessions that happened -- so only the strictly-after slice is removed.
            var toRemove = await DbContext.Events
                .Where(e => e.SeriesId == seriesId && e.Date > endDate)
                .ToListAsync(token);

            DbContext.Events.RemoveRange(toRemove);
            removedCount = toRemove.Count;
        }

        await DbContext.SaveChangesAsync(token);
        return removedCount;
    }

    /// <inheritdoc/>
    public async Task<bool> SetTemplateAsync(int seriesId, string title, string? description, TimeOnly? startTime, CancellationToken token = default)
    {
        var entity = await DbSet.FirstOrDefaultAsync(s => s.Id == seriesId, token);
        if (entity == null) return false;

        entity.Title = title;
        entity.Description = description;
        entity.StartTime = startTime;

        await DbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task DeleteWithOccurrencesAsync(int seriesId, CancellationToken token = default)
    {
        var series = await DbSet.FirstOrDefaultAsync(s => s.Id == seriesId, token);
        if (series == null) return;

        // The shipped foreign key from Events to EventSeries declares no delete behaviour and
        // therefore defaults to no action, so removing the series row while occurrences still
        // reference it throws. Removing the occurrences first is what makes the delete possible
        // at all, not tidiness. Their signup rows go with them through the existing cascade.
        var occurrences = await DbContext.Events
            .Where(e => e.SeriesId == seriesId)
            .ToListAsync(token);

        DbContext.Events.RemoveRange(occurrences);
        DbSet.Remove(series);

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task DetachOccurrencesAndDeleteAsync(int seriesId, CancellationToken token = default)
    {
        var series = await DbSet.FirstOrDefaultAsync(s => s.Id == seriesId, token);
        if (series == null) return;

        var occurrences = await DbContext.Events
            .Where(e => e.SeriesId == seriesId)
            .ToListAsync(token);

        // Both columns are cleared -- nulling only SeriesId would leave a row that claims a
        // slot of a series that no longer exists, and the filtered unique index would stop
        // covering it while the data still says it belongs to a series. CancelledAt is left
        // untouched: a cancelled one-off is a coherent state, and clearing it would silently
        // turn a session someone called off back into a live one.
        foreach (var occurrence in occurrences)
        {
            occurrence.SeriesId = null;
            occurrence.SeriesSlotIndex = null;
        }

        DbSet.Remove(series);

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task CreateWithOccurrencesAsync(EventSeries series, IReadOnlyList<Event> occurrences, IReadOnlyCollection<int> campaignMemberIds, CancellationToken token = default)
    {
        // A transaction is opened only when the provider is relational -- the in-memory
        // provider used by the unit tests rejects transactions outright. Held in a nullable
        // local and committed at the end if it exists.
        var transaction = DbContext.Database.IsRelational()
            ? await DbContext.Database.BeginTransactionAsync(token)
            : null;

        try
        {
            await AddAsync(series, token);

            foreach (var occurrence in occurrences)
            {
                occurrence.SeriesId = series.Id;

                // The existing fan-out method already leaves each automatic signup's answered
                // marker unset -- it is reused rather than reimplemented so that guarantee
                // cannot drift between two independent write paths.
                if (campaignMemberIds.Count > 0)
                {
                    await eventRepository.AddWithCampaignFanOutAsync(occurrence, campaignMemberIds, token);
                }
                else
                {
                    await eventRepository.AddAsync(occurrence, token);
                }
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(token);
            }
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
