using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class PlayerSignupRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<PlayerSignup, PlayerSignupEntity>(dbContext, mapper), IPlayerSignupRepository
{
    /// <inheritdoc/>
    public async Task<PlayerSignup?> GetByIdWithDateVotesAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet
            .Include(ps => ps.DateVotes)
            .FirstOrDefaultAsync(ps => ps.Id == id, cancellationToken);
        return entity == null ? null : Mapper.Map<PlayerSignup>(entity);
    }

    /// <inheritdoc/>
    public async Task<bool> ChangeVoteAsync(int playerSignupId, int proposedDateId, VoteType vote, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet
            .Include(ps => ps.DateVotes)
            .FirstOrDefaultAsync(ps => ps.Id == playerSignupId, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException("Player signup not found", nameof(playerSignupId));
        }

        var wasSelected = entity.IsSelected;

        // Only a selected Player-role signup frees a seat that counts against TotalPlayerCount;
        // the waitlist candidate pool and seat capacity are both scoped to Player.
        var wasSelectedPlayer = wasSelected && entity.SignupRole == (int)SignupRole.Player;

        var existingVote = entity.DateVotes.FirstOrDefault(dv => dv.ProposedDateId == proposedDateId);

        if (existingVote != null)
        {
            existingVote.Vote = (int)vote;
        }
        else
        {
            entity.DateVotes.Add(new PlayerDateVoteEntity
            {
                ProposedDateId = proposedDateId,
                PlayerSignupId = playerSignupId,
                Vote = (int)vote
            });
        }

        entity.LastVoteChangeTime = DateTime.UtcNow;

        if (vote == VoteType.No && wasSelected)
        {
            // A seat just freed. IsSelected for a Yes vote is decided by the caller based on a
            // freshly computed seat count — this method never rejects or auto-selects on capacity.
            entity.IsSelected = false;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return wasSelectedPlayer && vote == VoteType.No;
    }

    /// <inheritdoc/>
    public async Task<PlayerSignup?> GetTopWaitlistedCandidateAsync(int questId, int finalizedProposedDateId, CancellationToken cancellationToken = default)
    {
        var candidates = await DbSet
            .Include(ps => ps.DateVotes)
            .Where(ps => ps.QuestId == questId && !ps.IsSelected && ps.SignupRole == (int)SignupRole.Player)
            .ToListAsync(cancellationToken);

        var entity = candidates
            .OrderByDescending(ps => VotePriority(ps, finalizedProposedDateId))
            .ThenBy(ps => ps.LastVoteChangeTime ?? ps.SignupTime)
            .FirstOrDefault();

        return entity == null ? null : Mapper.Map<PlayerSignup>(entity);
    }

    private static int VotePriority(PlayerSignupEntity entity, int proposedDateId)
    {
        var vote = entity.DateVotes.FirstOrDefault(dv => dv.ProposedDateId == proposedDateId)?.Vote ?? (int)VoteType.No;
        return vote;
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(PlayerSignup model, CancellationToken token = default)
    {
        var entity = await DbSet
            .Include(ps => ps.DateVotes)
            .FirstOrDefaultAsync(ps => ps.Id == model.Id, token);
        if (entity == null) return;

        // Update scalar properties
        entity.IsSelected = model.IsSelected;
        entity.CharacterId = model.CharacterId;
        entity.SignupRole = (int)model.Role;

        // Update date votes
        entity.DateVotes.Clear();
        var dateVoteEntities = Mapper.Map<List<PlayerDateVoteEntity>>(model.DateVotes);
        foreach (var vote in dateVoteEntities)
        {
            entity.DateVotes.Add(vote);
        }

        await DbContext.SaveChangesAsync(token);
    }
}
