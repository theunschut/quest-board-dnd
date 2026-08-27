using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class EventSignupService(IEventSignupRepository repository, IMapper mapper) : BaseService<EventSignup>(repository, mapper), IEventSignupService
{
    /// <inheritdoc/>
    public async Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default)
    {
        await repository.SetAvailabilityAsync(eventId, userId, availability, token);
    }

    /// <inheritdoc/>
    public async Task<bool> WithdrawAsync(int eventId, int userId, CancellationToken token = default)
    {
        return await repository.WithdrawAsync(eventId, userId, token);
    }

    /// <inheritdoc/>
    public async Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default)
    {
        return await repository.GetRosterForEventAsync(eventId, token);
    }
}
