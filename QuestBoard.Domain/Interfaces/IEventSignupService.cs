using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IEventSignupService : IBaseService<EventSignup>
{
    /// <inheritdoc cref="IEventSignupRepository.SetAvailabilityAsync"/>
    Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default);

    /// <inheritdoc cref="IEventSignupRepository.WithdrawAsync"/>
    Task<bool> WithdrawAsync(int eventId, int userId, CancellationToken token = default);

    /// <inheritdoc cref="IEventSignupRepository.GetRosterForEventAsync"/>
    Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default);
}
