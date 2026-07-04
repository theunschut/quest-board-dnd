using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Interfaces;

public interface IPlayerSignupRepository : IBaseRepository<PlayerSignup>
{
    /// <summary>
    /// Returns a single player signup with its date votes loaded.
    /// </summary>
    Task<PlayerSignup?> GetByIdWithDateVotesAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a player's vote for the given proposed date, resetting the waitlist ordering
    /// timestamp on every call. Never rejects on capacity — the caller decides selection for a
    /// Yes vote based on a freshly computed seat count. Returns true when a seat was just freed
    /// (the signup was previously selected and the new vote is No), signaling the caller should
    /// look for a waitlisted candidate to promote.
    /// </summary>
    Task<bool> ChangeVoteAsync(int playerSignupId, int proposedDateId, VoteType vote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the top-priority waitlisted player signup for the finalized proposed date
    /// (Yes votes first, then Maybe, then No/none), broken by the earliest
    /// last-vote-change time, falling back to signup time. Returns null when no waitlisted
    /// player signup exists.
    /// </summary>
    Task<PlayerSignup?> GetTopWaitlistedCandidateAsync(int questId, int finalizedProposedDateId, CancellationToken cancellationToken = default);
}
