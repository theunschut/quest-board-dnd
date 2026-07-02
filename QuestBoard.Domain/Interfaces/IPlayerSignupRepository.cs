using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Interfaces;

public interface IPlayerSignupRepository : IBaseRepository<PlayerSignup>
{
    /// <summary>
    /// Returns a single player signup with its date votes loaded.
    /// </summary>
    Task<PlayerSignup?> GetByIdWithDateVotesAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a player's vote for the given proposed date to Yes and marks the signup as selected.
    /// </summary>
    Task ChangeVoteToYesAndSelectAsync(int playerSignupId, int proposedDateId, CancellationToken cancellationToken = default);
}
