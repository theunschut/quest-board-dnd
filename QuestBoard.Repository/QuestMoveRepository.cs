using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal sealed class QuestMoveRepository(QuestBoardContext dbContext) : IQuestMoveRepository
{
    /// <inheritdoc/>
    public async Task<MoveStatus> ApplyAsync(QuestMovePlan plan, CancellationToken token = default)
    {
        // Every read happens before GroupId changes: the board filter hides a quest's signups and
        // votes once the quest sits on another board, so nothing can be loaded after the move.
        var quest = await dbContext.Quests
            .Include(q => q.PlayerSignups)
                .ThenInclude(ps => ps.DateVotes)
            .FirstOrDefaultAsync(q => q.Id == plan.QuestId, token);

        if (quest == null || quest.GroupId != plan.SourceGroupId)
        {
            return MoveStatus.NotFound;
        }

        // Loaded now, before any change, because the board filter hides them once the quest moves.
        var followUps = await dbContext.Quests
            .Where(q => q.OriginalQuestId == plan.QuestId)
            .ToListAsync(token);

        // The plan was built from an earlier read. If the signups, the follow-ups or the original
        // link changed since, applying it would keep, drop or cut the wrong things, so nothing is
        // touched.
        var loadedIds = quest.PlayerSignups.Select(s => s.Id).OrderBy(id => id).ToList();
        var plannedIds = plan.KeptSignupIds.Concat(plan.DroppedSignupIds).OrderBy(id => id).ToList();
        if (!loadedIds.SequenceEqual(plannedIds))
        {
            return MoveStatus.Changed;
        }

        var loadedFollowUpIds = followUps.Select(f => f.Id).OrderBy(id => id).ToList();
        var plannedFollowUpIds = plan.FollowUpQuestIdsToUnlink.OrderBy(id => id).ToList();
        if (!loadedFollowUpIds.SequenceEqual(plannedFollowUpIds))
        {
            return MoveStatus.Changed;
        }

        if (plan.ClearOriginalLink != (quest.OriginalQuestId != null))
        {
            return MoveStatus.Changed;
        }

        var keptIds = plan.KeptSignupIds.ToHashSet();
        if (plan.PromotedSignupIds.Any(id => !keptIds.Contains(id)))
        {
            return MoveStatus.Changed;
        }

        // The ids can match while what they stand for has not: a seat given or taken, a role
        // switched or a vote changed since the plan was drawn would make the plan keep, drop or
        // promote the wrong people.
        if (!SignupsStillMatchThePlan(quest.PlayerSignups, plan))
        {
            return MoveStatus.Changed;
        }

        // Board membership is read here too, because it decides who is dropped. A player added to
        // or removed from the target since the plan was drawn must not be dropped or kept wrongly.
        var playerIds = quest.PlayerSignups.Select(s => s.PlayerId).Distinct().ToList();
        var targetMemberIds = (await dbContext.UserGroups
                .Where(ug => ug.GroupId == plan.TargetGroupId && playerIds.Contains(ug.UserId))
                .Select(ug => ug.UserId)
                .ToListAsync(token))
            .ToHashSet();
        var droppedIdSet = plan.DroppedSignupIds.ToHashSet();
        if (quest.PlayerSignups.Any(s => targetMemberIds.Contains(s.PlayerId) == droppedIdSet.Contains(s.Id)))
        {
            return MoveStatus.Changed;
        }

        var droppedIds = plan.DroppedSignupIds.ToHashSet();
        var promotedIds = plan.PromotedSignupIds.ToHashSet();
        foreach (var signup in quest.PlayerSignups.ToList())
        {
            if (droppedIds.Contains(signup.Id))
            {
                // The votes are removed explicitly so the result is the same whether or not the
                // database cascades the delete.
                dbContext.Set<PlayerDateVoteEntity>().RemoveRange(signup.DateVotes);
                dbContext.PlayerSignups.Remove(signup);
                continue;
            }

            // A character belongs to the board it was made on, so it never travels with the signup.
            signup.CharacterId = null;
            if (promotedIds.Contains(signup.Id))
            {
                signup.IsSelected = true;
            }
        }

        // Both ends of a chain link are cut, so no hidden link is left behind that would block a
        // new follow-up on the source board.
        if (plan.ClearOriginalLink)
        {
            quest.OriginalQuestId = null;
        }

        foreach (var followUp in followUps)
        {
            followUp.OriginalQuestId = null;
        }

        // Assigned last, so a refusal above leaves the tracked graph untouched.
        quest.GroupId = plan.TargetGroupId;

        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return MoveStatus.Changed;
        }
        catch
        {
            // No half-applied tracked graph may be saved later in the same request.
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return MoveStatus.Moved;
    }

    // True only when every loaded signup is the one the plan was drawn from, and every promotion
    // still fills a seat the way the waitlist rule would: unselected, a player, voted Yes or Maybe
    // for the finalized date.
    private static bool SignupsStillMatchThePlan(IEnumerable<PlayerSignupEntity> loaded, QuestMovePlan plan)
    {
        var signups = loaded.ToList();
        var basisById = plan.SignupBasis.GroupBy(b => b.SignupId).ToDictionary(g => g.Key, g => g.First());
        if (basisById.Count != plan.SignupBasis.Count || basisById.Count != signups.Count)
        {
            return false;
        }

        VoteType? VoteFor(PlayerSignupEntity signup) => plan.FinalizedProposedDateId is not { } dateId
            ? null
            : signup.DateVotes.FirstOrDefault(v => v.ProposedDateId == dateId)?.Vote is { } vote ? (VoteType)vote : null;

        foreach (var signup in signups)
        {
            if (!basisById.TryGetValue(signup.Id, out var basis)
                || basis.PlayerId != signup.PlayerId
                || basis.IsSelected != signup.IsSelected
                || (int)basis.Role != signup.SignupRole
                || basis.FinalizedDateVote != VoteFor(signup))
            {
                return false;
            }
        }

        var promotedIds = plan.PromotedSignupIds.ToHashSet();
        return signups
            .Where(s => promotedIds.Contains(s.Id))
            .All(s => !s.IsSelected
                && s.SignupRole == (int)SignupRole.Player
                && VoteFor(s) is VoteType.Yes or VoteType.Maybe);
    }
}
