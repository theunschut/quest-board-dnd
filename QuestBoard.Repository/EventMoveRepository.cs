using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal sealed class EventMoveRepository(QuestBoardContext dbContext) : IEventMoveRepository
{
    /// <inheritdoc/>
    public async Task<MoveStatus> ApplyAsync(EventMovePlan plan, CancellationToken token = default)
    {
        // Every read happens before GroupId changes: the board filter hides an event's signups
        // once the event sits on another board, so nothing can be loaded after the move.
        var evt = await dbContext.Events
            .Include(e => e.Signups)
            .FirstOrDefaultAsync(e => e.Id == plan.EventId, token);

        if (evt == null || evt.GroupId != plan.SourceGroupId)
        {
            return MoveStatus.NotFound;
        }

        // The plan was built from an earlier read. If the answers changed since, applying it
        // would keep or drop the wrong rows, or add a second row for someone, so nothing is touched.
        var loadedIds = evt.Signups.Select(s => s.Id).OrderBy(id => id).ToList();
        var plannedIds = plan.KeptSignupIds.Concat(plan.DroppedSignupIds).OrderBy(id => id).ToList();
        if (!loadedIds.SequenceEqual(plannedIds))
        {
            return MoveStatus.Changed;
        }

        var existingUserIds = evt.Signups.Select(s => s.UserId).ToHashSet();
        if (plan.CampaignAddUserIds.Any(existingUserIds.Contains))
        {
            return MoveStatus.Changed;
        }

        var droppedIds = plan.DroppedSignupIds.ToHashSet();
        foreach (var signup in evt.Signups.Where(s => droppedIds.Contains(s.Id)).ToList())
        {
            dbContext.EventSignups.Remove(signup);
        }

        // The same automatic row a new Campaign event gets for each member. Leaving the answered
        // marker unset is what keeps it distinguishable from an answer a person actually gave.
        foreach (var userId in plan.CampaignAddUserIds.Distinct())
        {
            evt.Signups.Add(new EventSignupEntity
            {
                UserId = userId,
                Availability = (int)VoteType.Yes
            });
        }

        // Assigned last, so a refusal above leaves the tracked graph untouched.
        evt.GroupId = plan.TargetGroupId;

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
