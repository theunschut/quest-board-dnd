using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Interfaces;

public interface IPlayerSignupService : IBaseService<PlayerSignup>
{
    /// <summary>
    /// Replaces a player signup's date votes with the given set.
    /// Throws InvalidOperationException if the signup belongs to a Spectator (spectators cannot vote).
    /// </summary>
    Task UpdatePlayerDateVotesAsync(int playerSignupId, List<PlayerDateVote> dateVotes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears (when characterId is null) the character attached to a player signup.
    /// </summary>
    Task UpdateSignupCharacterAsync(int playerSignupId, int? characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a player's vote for the given proposed date to Yes and marks the signup as selected.
    /// </summary>
    Task ChangeVoteToYesAndSelectAsync(int playerSignupId, int proposedDateId, CancellationToken cancellationToken = default);
}