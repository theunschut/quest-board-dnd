using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using Microsoft.Extensions.Options;

namespace QuestBoard.Domain.Services;

internal class EventSeriesService(
    IEventSeriesRepository repository,
    IEventRepository eventRepository,
    IUserRepository userRepository,
    IBoardTypeResolver boardTypeResolver,
    IActiveGroupContext activeGroupContext,
    IOptions<EventSeriesOptions> options) : IEventSeriesService
{
    /// <inheritdoc/>
    public Task<(IReadOnlyList<DateOnly> Dates, bool AnchorFullyInPast)> PreviewAsync(DateOnly anchorDate, int intervalWeeks, string cycleMask, DateOnly? endDate, CancellationToken token = default)
    {
        var mask = EventSeriesDateGenerator.ParseMask(cycleMask);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var previewCount = options.Value.PreviewCount;

        // Materialized once so the two views below (the anchor-relative window and the
        // today-or-later window) don't each re-run the generator.
        var firingSlots = EventSeriesDateGenerator
            .GenerateSlots(anchorDate, intervalWeeks, mask, endDate, EventSeriesDateGenerator.MaxSlotScan)
            .Where(slot => slot.Fires)
            .ToList();

        // Whether the first window of firing dates counted from the anchor itself are all
        // already behind today -- this is what lets the caller explain that the dates shown
        // start from today rather than from the anchor the DM typed in.
        var anchorWindow = firingSlots.Take(previewCount).ToList();
        var anchorFullyInPast = anchorWindow.Count > 0 && anchorWindow.All(slot => slot.Date < today);

        var dates = firingSlots
            .Where(slot => slot.Date >= today)
            .Take(previewCount)
            .Select(slot => slot.Date)
            .ToList();

        return Task.FromResult<(IReadOnlyList<DateOnly>, bool)>((dates, anchorFullyInPast));
    }

    /// <inheritdoc/>
    public async Task<EventSeries> CreateWithFirstPassAsync(EventSeries series, CancellationToken token = default)
    {
        // WeekDay is derived from the anchor, never independently editable.
        series.WeekDay = (int)series.AnchorDate.DayOfWeek;

        // A series with no active board is not a state this method may invent -- it fails
        // closed rather than guessing a group.
        var groupId = activeGroupContext.RequireActiveGroupId();
        series.GroupId = groupId;

        var mask = EventSeriesDateGenerator.ParseMask(series.CycleMask);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Only slots dated today or later are materialized; earlier slots are still walked by
        // the generator so slot numbering and cycle phase stay correct, but they are never
        // turned into rows.
        var firstPassSlots = EventSeriesDateGenerator
            .GenerateSlots(series.AnchorDate, series.IntervalWeeks, mask, series.EndDate, EventSeriesDateGenerator.MaxSlotScan)
            .Where(slot => slot.Fires && slot.Date >= today)
            .Take(options.Value.RunwaySize)
            .ToList();

        var boardType = await boardTypeResolver.GetBoardTypeAsync(token);
        var isCampaign = boardType == BoardType.Campaign;
        var memberIds = isCampaign
            ? (await userRepository.GetAllGroupMembers(groupId, token)).Select(member => member.Id).ToList()
            : [];

        var occurrences = firstPassSlots
            .Select(slot => BuildOccurrenceFromTemplate(series, slot.SlotIndex, slot.Date))
            .ToList();

        // One transaction for the series row and the whole first pass -- the repository stamps
        // SeriesId onto each occurrence itself, and reuses the shipped campaign fan-out rather
        // than a second signup-creation path.
        await repository.CreateWithOccurrencesAsync(series, occurrences, memberIds, token);

        return series;
    }

    /// <inheritdoc/>
    public async Task<int> TopUpAsync(int seriesId, CancellationToken token = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var series = await repository.GetSeriesAsync(seriesId, token);
        if (series == null || (series.EndDate.HasValue && series.EndDate.Value < today))
        {
            return 0;
        }

        var mask = EventSeriesDateGenerator.ParseMask(series.CycleMask);

        // Every slot the series has ever produced, cancelled and moved occurrences included,
        // with no date predicate -- a windowed read would let a slot moved far outside the
        // window read as free and regenerate on its original date.
        var existingSlots = (await repository.GetSlotIndexesForSeriesAsync(seriesId, token)).ToHashSet();

        var liveFutureCount = await repository.CountLiveFutureOccurrencesAsync(seriesId, today, token);
        var runwayTarget = options.Value.RunwaySize;
        if (liveFutureCount >= runwayTarget)
        {
            return 0;
        }

        var groupId = activeGroupContext.RequireActiveGroupId();
        var boardType = await boardTypeResolver.GetBoardTypeAsync(token);
        var isCampaign = boardType == BoardType.Campaign;
        var memberIds = isCampaign
            ? (await userRepository.GetAllGroupMembers(groupId, token)).Select(member => member.Id).ToList()
            : [];

        var created = 0;
        foreach (var slot in EventSeriesDateGenerator.GenerateSlots(series.AnchorDate, series.IntervalWeeks, mask, series.EndDate, EventSeriesDateGenerator.MaxSlotScan))
        {
            if (liveFutureCount >= runwayTarget)
            {
                break;
            }

            if (!slot.Fires || slot.Date < today)
            {
                continue;
            }

            // The membership check runs immediately before every single write, not once at the
            // top of the loop -- so a retry after a mid-run crash finds the earlier occurrences
            // already present and only creates the rest, keeping progress monotonic.
            if (existingSlots.Contains(slot.SlotIndex))
            {
                continue;
            }

            var occurrence = BuildOccurrenceFromTemplate(series, slot.SlotIndex, slot.Date);
            occurrence.SeriesId = series.Id;

            if (isCampaign)
            {
                await eventRepository.AddWithCampaignFanOutAsync(occurrence, memberIds, token);
            }
            else
            {
                await eventRepository.AddAsync(occurrence, token);
            }

            // The slot goes into the set the instant it's written -- a cancelled occurrence's
            // slot is already in this set from the read above, so cancelling never causes a
            // re-creation while still freeing a runway slot for the next candidate.
            existingSlots.Add(slot.SlotIndex);
            liveFutureCount++;
            created++;
        }

        return created;
    }

    /// <inheritdoc/>
    public async Task<EventSeries?> GetSeriesAsync(int seriesId, CancellationToken token = default)
    {
        return await repository.GetSeriesAsync(seriesId, token);
    }

    /// <inheritdoc/>
    public async Task<IList<EventSeries>> GetActiveSeriesForActiveGroupAsync(CancellationToken token = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await repository.GetActiveSeriesAsync(today, token);
    }

    /// <inheritdoc/>
    public async Task<IList<Event>> GetOccurrencesAsync(int seriesId, CancellationToken token = default)
    {
        return await eventRepository.GetOccurrencesForSeriesAsync(seriesId, token);
    }

    /// <inheritdoc/>
    public async Task<IList<SeriesRunwayStatus>> GetSeriesBelowRunwayAsync(CancellationToken token = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await repository.GetSeriesBelowRunwayAsync(today, options.Value.RunwaySize, token);
    }

    /// <inheritdoc/>
    public async Task<SeriesRemovalImpact> GetRemovalImpactAsync(int seriesId, CancellationToken token = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await repository.GetRemovalImpactAsync(seriesId, today, token);
    }

    /// <inheritdoc/>
    public async Task<int> CountLiveSiblingsOnDateAsync(int seriesId, DateOnly date, int excludeEventId, CancellationToken token = default)
    {
        return await eventRepository.CountLiveSiblingsOnDateAsync(seriesId, date, excludeEventId, token);
    }

    /// <inheritdoc/>
    public async Task<int> EndAsync(int seriesId, DateOnly endDate, bool removeFutureOccurrences, CancellationToken token = default)
    {
        return await repository.SetEndDateAsync(seriesId, endDate, removeFutureOccurrences, token);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int seriesId, CancellationToken token = default)
    {
        await repository.DeleteWithOccurrencesAsync(seriesId, token);
    }

    /// <inheritdoc/>
    public async Task DetachAsync(int seriesId, CancellationToken token = default)
    {
        await repository.DetachOccurrencesAndDeleteAsync(seriesId, token);
    }

    /// <inheritdoc/>
    public async Task<int> ApplyTemplateToFutureAsync(int seriesId, int editedEventId, string title, string? description, TimeOnly? startTime, CancellationToken token = default)
    {
        var series = await repository.GetSeriesAsync(seriesId, token);
        if (series == null)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var occurrences = await eventRepository.GetOccurrencesForSeriesAsync(seriesId, token);

        // Eligibility is computed against the OLD template, before the series row below is
        // touched -- comparing against the new template would make every occurrence trivially
        // "still match" and the separately-edited exclusion could never fire.
        var eligibleIds = occurrences
            .Where(occurrence =>
                occurrence.Id != editedEventId &&
                occurrence.Date >= today &&
                !occurrence.IsCancelled &&
                occurrence.SeriesSlotIndex.HasValue &&
                EventSeriesDateGenerator.DateForSlot(series.AnchorDate, series.IntervalWeeks, occurrence.SeriesSlotIndex.Value) == occurrence.Date &&
                occurrence.Title == series.Title &&
                occurrence.Description == series.Description &&
                occurrence.StartTime == series.StartTime)
            .Select(occurrence => occurrence.Id)
            .ToList();

        // So newly generated slots inherit the change too.
        await repository.SetTemplateAsync(seriesId, title, description, startTime, token);

        return await eventRepository.ApplyTemplateToOccurrencesAsync(eligibleIds, title, description, startTime, token);
    }

    private static Event BuildOccurrenceFromTemplate(EventSeries series, int slotIndex, DateOnly date) => new()
    {
        Title = series.Title,
        Description = series.Description,
        StartTime = series.StartTime,
        Date = date,
        SeriesSlotIndex = slotIndex,
        GroupId = series.GroupId
    };
}
