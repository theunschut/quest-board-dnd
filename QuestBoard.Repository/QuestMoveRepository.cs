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

        // The plan was built from an earlier read. If the signups changed since, applying it
        // would keep or drop the wrong people, so nothing is touched.
        var loadedIds = quest.PlayerSignups.Select(s => s.Id).OrderBy(id => id).ToList();
        var plannedIds = plan.KeptSignupIds.Concat(plan.DroppedSignupIds).OrderBy(id => id).ToList();
        if (!loadedIds.SequenceEqual(plannedIds))
        {
            return MoveStatus.Changed;
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
