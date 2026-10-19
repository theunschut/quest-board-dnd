using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Services;

internal class QuestMoveService(
    IQuestRepository questRepository,
    IActiveGroupContext activeGroupContext,
    ILinkedBoardSetRepository linkedBoardSetRepository,
    IQuestMoveRepository moveRepository,
    IBoardClock boardClock,
    ILogger<QuestMoveService> logger) : IQuestMoveService
{
    /// <inheritdoc/>
    public Task<QuestMovePreview> PreviewAsync(int questId, int? targetBoardId, int actorUserId, CancellationToken token = default) =>
        BuildPreviewAsync(questId, targetBoardId, actorUserId, token);

    /// <inheritdoc/>
    public async Task<QuestMovePreview> MoveAsync(int questId, int targetBoardId, string stamp, int actorUserId, CancellationToken token = default)
    {
        // Nothing from the page is trusted: the whole preview is rebuilt from the store here.
        var preview = await BuildPreviewAsync(questId, targetBoardId, actorUserId, token);
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
            logger.LogError(ex, "Moving quest {QuestId} from board {SourceGroupId} to board {TargetGroupId} failed",
                questId, plan.SourceGroupId, plan.TargetGroupId);
            outcome = MoveStatus.Failed;
        }

        if (outcome == MoveStatus.Changed)
        {
            // The store moved on between the recompute and the write, so show what it holds now.
            var fresh = await BuildPreviewAsync(questId, targetBoardId, actorUserId, token);
            if (fresh.Status == MoveStatus.Ready)
            {
                fresh.Status = MoveStatus.Changed;
            }

            return fresh;
        }

        if (outcome == MoveStatus.Moved)
        {
            logger.LogInformation(
                "Moved quest {QuestId} from board {SourceGroupId} to board {TargetGroupId} by user {ActorUserId}: kept {Kept}, dropped {Dropped}, promoted {Promoted}, links cut {LinksCut}",
                questId, plan.SourceGroupId, plan.TargetGroupId, actorUserId,
                plan.KeptSignupIds.Count, plan.DroppedSignupIds.Count, plan.PromotedSignupIds.Count,
                (plan.ClearOriginalLink ? 1 : 0) + plan.FollowUpQuestIdsToUnlink.Count);
        }

        preview.Status = outcome;
        return preview;
    }

    private async Task<QuestMovePreview> BuildPreviewAsync(int questId, int? targetBoardId, int actorUserId, CancellationToken token)
    {
        if (activeGroupContext.ActiveGroupId is not { } sourceGroupId)
        {
            return new QuestMovePreview { Status = MoveStatus.NotFound, EntryId = questId };
        }

        // The snapshot is scoped to the source board, so a quest that lives elsewhere reads as missing.
        var quest = await questRepository.GetQuestWithDetailsAsync(questId, token);
        if (quest == null)
        {
            return new QuestMovePreview { Status = MoveStatus.NotFound, EntryId = questId, SourceGroupId = sourceGroupId };
        }

        var sourceName = (await linkedBoardSetRepository.GetBoardAsync(sourceGroupId, token))?.Name ?? string.Empty;

        var preview = new QuestMovePreview
        {
            EntryId = quest.Id,
            EntryTitle = quest.Title,
            SourceGroupId = sourceGroupId,
            SourceBoardName = sourceName,
            Quest = quest,
            DmName = quest.DungeonMaster?.Name ?? "Unknown DM",
            DungeonMasterId = quest.DungeonMasterId
        };

        preview.BlockReason = MoveEligibility.ForQuest(quest, boardClock.Today);
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

        var keptIds = quest.PlayerSignups.Select(s => s.Id).ToList();
        var plan = new QuestMovePlan(
            quest.Id,
            sourceGroupId,
            target.Id,
            keptIds,
            DroppedSignupIds: [],
            PromotedSignupIds: [],
            ClearOriginalLink: false,
            FollowUpQuestIdsToUnlink: []);
        preview.Plan = plan;
        preview.KeptCount = keptIds.Count;

        var unlinked = plan.ClearOriginalLink
            ? plan.FollowUpQuestIdsToUnlink.Append(quest.Id)
            : plan.FollowUpQuestIdsToUnlink;
        preview.Stamp = MovePreviewStamp.Compute(
            "quest", quest.Id, target.Id,
            plan.KeptSignupIds, plan.DroppedSignupIds, plan.PromotedSignupIds, unlinked, []);
        preview.Status = MoveStatus.Ready;
        return preview;
    }
}
