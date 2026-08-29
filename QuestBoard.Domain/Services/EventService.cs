using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class EventService(IEventRepository repository, IMapper mapper) : BaseService<Event>(repository, mapper), IEventService
{
    /// <inheritdoc/>
    public async Task<IList<Event>> GetEventsForCalendarAsync(CancellationToken token = default)
    {
        return await repository.GetEventsForCalendarAsync(token);
    }

    /// <inheritdoc/>
    public async Task<Event?> GetEventWithDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetEventWithDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task<int?> GetSeriesGroupIdAsync(int seriesId, CancellationToken token = default)
    {
        return await repository.GetSeriesGroupIdAsync(seriesId, token);
    }

    /// <inheritdoc/>
    public async Task AddWithCampaignFanOutAsync(Event newEvent, IEnumerable<int> memberIds, CancellationToken token = default)
    {
        await repository.AddWithCampaignFanOutAsync(newEvent, memberIds, token);
    }

    /// <inheritdoc/>
    public async Task<bool> SetCancelledAsync(int eventId, DateTime? cancelledAt, CancellationToken token = default)
    {
        return await repository.SetCancelledAsync(eventId, cancelledAt, token);
    }

    /// <inheritdoc/>
    public Task<EventAvailabilityOverview> GetAvailabilityOverviewAsync(int take, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}
