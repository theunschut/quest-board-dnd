using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Services;

// An event move emails nobody, so this service deliberately has no mail dispatcher.
internal class EventMoveService(
    IEventRepository eventRepository,
    IEventSignupRepository eventSignupRepository,
    IUserRepository userRepository,
    IActiveGroupContext activeGroupContext,
    ILinkedBoardSetRepository linkedBoardSetRepository,
    IEventMoveRepository moveRepository,
    IBoardClock boardClock,
    ILogger<EventMoveService> logger) : IEventMoveService
{
    /// <inheritdoc/>
    public Task<EventMovePreview> PreviewAsync(int eventId, int? targetBoardId, int actorUserId, CancellationToken token = default) =>
        BuildPreviewAsync(eventId, targetBoardId, actorUserId, token);

    /// <inheritdoc/>
    public async Task<EventMovePreview> MoveAsync(int eventId, int targetBoardId, string stamp, int actorUserId, CancellationToken token = default)
    {
        // Nothing from the page is trusted: the whole preview is rebuilt from the store here.
        var preview = await BuildPreviewAsync(eventId, targetBoardId, actorUserId, token);
        if (preview.Status != MoveStatus.Ready || preview.Plan is not { } plan)
        {
            return preview;
        }

        if (!string.Equals(stamp, preview.Stamp, StringComparison.Ordinal))
        {
            preview.Status = MoveStatus.Changed;
            return preview;
        }

        MoveStatus outcome;
        try
        {
            outcome = await moveRepository.ApplyAsync(plan, token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Moving event {EventId} from board {SourceGroupId} to board {TargetGroupId} failed",
                eventId, plan.SourceGroupId, plan.TargetGroupId);
            outcome = MoveStatus.Failed;
        }

        if (outcome == MoveStatus.Changed)
        {
            // The store moved on between the recompute and the write, so show what it holds now.
            var fresh = await BuildPreviewAsync(eventId, targetBoardId, actorUserId, token);
            if (fresh.Status == MoveStatus.Ready)
            {
                fresh.Status = MoveStatus.Changed;
            }

            return fresh;
        }

        if (outcome == MoveStatus.Moved)
        {
            logger.LogInformation(
                "Moved event {EventId} from board {SourceGroupId} to board {TargetGroupId} by user {ActorUserId}: kept {Kept}, dropped {Dropped}, added {Added}",
                eventId, plan.SourceGroupId, plan.TargetGroupId, actorUserId,
                plan.KeptSignupIds.Count, plan.DroppedSignupIds.Count, plan.CampaignAddUserIds.Count);
        }

        preview.Status = outcome;
        return preview;
    }

    private async Task<EventMovePreview> BuildPreviewAsync(int eventId, int? targetBoardId, int actorUserId, CancellationToken token)
    {
        if (activeGroupContext.ActiveGroupId is not { } sourceGroupId)
        {
            return new EventMovePreview { Status = MoveStatus.NotFound, EntryId = eventId };
        }

        // The snapshot is scoped to the source board, so an event that lives elsewhere reads as missing.
        var evt = await eventRepository.GetEventWithDetailsAsync(eventId, token);
        if (evt == null)
        {
            return new EventMovePreview { Status = MoveStatus.NotFound, EntryId = eventId, SourceGroupId = sourceGroupId };
        }

        var sourceName = (await linkedBoardSetRepository.GetBoardAsync(sourceGroupId, token))?.Name ?? string.Empty;

        var preview = new EventMovePreview
        {
            EntryId = evt.Id,
            EntryTitle = evt.Title,
            SourceGroupId = sourceGroupId,
            SourceBoardName = sourceName,
            Event = evt
        };

        preview.BlockReason = MoveEligibility.ForEvent(evt, boardClock.Today);
        if (preview.BlockReason != MoveBlockReason.None)
        {
            preview.Status = MoveStatus.Blocked;
            return preview;
        }

        // Read fresh on every call: a set edited a moment ago must not leave a stale target allowed.
        var linked = await linkedBoardSetRepository.GetLinkedBoardsForViewerAsync(sourceGroupId, actorUserId, token);
        preview.LinkedBoards = linked.Boards;
        preview.TargetBoardType = linked.SetBoardType;

        if (linked.Boards.Count == 0)
        {
            preview.Status = MoveStatus.NoLinkedBoards;
            return preview;
        }

        if (targetBoardId == null)
        {
            if (linked.Boards.Count != 1)
            {
                preview.Status = MoveStatus.ChooseTarget;
                return preview;
            }

            targetBoardId = linked.Boards[0].Id;
        }

        // The source board is never in the linked list, so a posted source id is refused here too.
        var target = linked.Boards.FirstOrDefault(b => b.Id == targetBoardId);
        if (target == null)
        {
            preview.Status = MoveStatus.TargetNotLinked;
            return preview;
        }

        preview.Target = target;

        var roster = await eventSignupRepository.GetRosterForEventAsync(evt.Id, token);

        // Membership means any role on the target board, and needs no filter bypass.
        var memberIds = (await userRepository.GetAllGroupMembers(target.Id, token)).Select(u => u.Id).ToHashSet();
        var kept = roster.Where(r => memberIds.Contains(r.UserId)).ToList();
        var dropped = roster.Where(r => !memberIds.Contains(r.UserId)).ToList();

        preview.Dropped = dropped
            .OrderBy(r => r.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(r => new MovePerson(r.UserId, r.UserName, null, false))
            .ToList();

        // Only a Campaign board gives every member an automatic Yes row; a One-Shot event starts
        // with no rows at all.
        var rosterUserIds = roster.Select(r => r.UserId).ToHashSet();
        var campaignAdds = linked.SetBoardType == BoardType.Campaign
            ? memberIds.Where(id => !rosterUserIds.Contains(id)).Distinct().Order().ToList()
            : [];

        var plan = new EventMovePlan(
            evt.Id,
            sourceGroupId,
            target.Id,
            kept.Select(r => r.Id).ToList(),
            dropped.Select(r => r.Id).ToList(),
            campaignAdds);
        preview.Plan = plan;
        preview.KeptCount = kept.Count;
        preview.CampaignAddCount = campaignAdds.Count;

        preview.Stamp = MovePreviewStamp.Compute(
            "event", evt.Id, target.Id,
            plan.KeptSignupIds, plan.DroppedSignupIds, [], [], plan.CampaignAddUserIds);
        preview.Status = MoveStatus.Ready;
        return preview;
    }
}
