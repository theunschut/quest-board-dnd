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
}
