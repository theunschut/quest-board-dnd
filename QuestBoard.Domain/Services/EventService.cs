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
}
