using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Services;

internal class PlayerSignupService(IPlayerSignupRepository repository, IMapper mapper) : BaseService<PlayerSignup>(repository, mapper), IPlayerSignupService
{
    /// <inheritdoc/>
    public async Task UpdatePlayerDateVotesAsync(int playerSignupId, List<PlayerDateVote> dateVotes, CancellationToken cancellationToken = default)
    {
        // Get the existing player signup with its date votes
        var playerSignup = await repository.GetByIdWithDateVotesAsync(playerSignupId, cancellationToken);
        if (playerSignup == null)
        {
            throw new ArgumentException("Player signup not found", nameof(playerSignupId));
        }

        // Validate: Spectators cannot vote (SignupRole == Spectator)
        if (playerSignup.Role == QuestBoard.Domain.Enums.SignupRole.Spectator)
        {
            throw new InvalidOperationException("Spectators cannot vote on dates");
        }

        // Replace date votes on the domain model
        playerSignup.DateVotes = dateVotes;
        foreach (var vote in playerSignup.DateVotes)
        {
            vote.PlayerSignupId = playerSignupId;
        }

        await repository.UpdateAsync(playerSignup, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateSignupCharacterAsync(int playerSignupId, int? characterId, CancellationToken cancellationToken = default)
    {
        var playerSignup = await repository.GetByIdAsync(playerSignupId, cancellationToken);
        if (playerSignup == null)
        {
            throw new ArgumentException("Player signup not found", nameof(playerSignupId));
        }

        playerSignup.CharacterId = characterId;
        await repository.UpdateAsync(playerSignup, cancellationToken);
    }
}
