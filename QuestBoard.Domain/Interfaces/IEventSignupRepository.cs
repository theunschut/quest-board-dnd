using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IEventSignupRepository : IBaseRepository<EventSignup>
{
    /// <summary>
    /// Creates the caller's signup row for the event when none exists yet, or updates it when
    /// one does, stamping the answered timestamp in both cases. The caller must supply
    /// <paramref name="userId"/> from the authenticated principal and never from request input,
    /// and <paramref name="eventId"/> must identify an event on the active board.
    /// </summary>
    Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default);

    /// <summary>
    /// Deletes the caller's signup row for the event, if one exists, returning whether a row was
    /// removed. This is the only write path in the feature that removes a row, and is what
    /// preserves a genuine not-answered state distinct from any of the three vote values.
    /// Restricting withdrawal to boards where it makes sense is the caller's responsibility,
    /// not this method's.
    /// </summary>
    Task<bool> WithdrawAsync(int eventId, int userId, CancellationToken token = default);

    /// <summary>
    /// Returns every signup row on the event with the member's name populated, ordered by name.
    /// Uses a single query with an eager include so the roster costs one round trip rather than
    /// one query per row.
    /// </summary>
    Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default);
}
