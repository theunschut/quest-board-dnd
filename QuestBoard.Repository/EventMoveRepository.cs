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

        // The ids can match while whose answer they are has not.
        var basisUserIds = plan.SignupBasis.GroupBy(b => b.SignupId).ToDictionary(g => g.Key, g => g.First().UserId);
        if (basisUserIds.Count != plan.SignupBasis.Count
            || evt.Signups.Any(s => !basisUserIds.TryGetValue(s.Id, out var userId) || userId != s.UserId))
        {
            return MoveStatus.Changed;
        }

        // Board membership decides who is dropped and who is added, so it is read here too. Someone
        // added to or removed from the target since the plan was drawn must not be dropped, kept
        // or given an automatic row wrongly.
        var relevantUserIds = evt.Signups.Select(s => s.UserId).Concat(plan.CampaignAddUserIds).Distinct().ToList();
        var targetMemberIds = (await dbContext.UserGroups
                .Where(ug => ug.GroupId == plan.TargetGroupId && relevantUserIds.Contains(ug.UserId))
                .Select(ug => ug.UserId)
                .ToListAsync(token))
            .ToHashSet();
        var plannedDroppedIds = plan.DroppedSignupIds.ToHashSet();
        if (evt.Signups.Any(s => targetMemberIds.Contains(s.UserId) == plannedDroppedIds.Contains(s.Id))
            || plan.CampaignAddUserIds.Any(userId => !targetMemberIds.Contains(userId)))
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
