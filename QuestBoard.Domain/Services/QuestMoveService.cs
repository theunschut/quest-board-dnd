using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Services;

internal class QuestMoveService(
    IQuestRepository questRepository,
    IActiveGroupContext activeGroupContext,
    ILinkedBoardSetRepository linkedBoardSetRepository,
    IQuestMoveRepository moveRepository,
    ILogger<QuestMoveService> logger) : IQuestMoveService
{
    /// <inheritdoc/>
    public async Task<QuestMovePreview> MoveAsync(int questId, int targetBoardId, int actorUserId, CancellationToken token = default)
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

        // Read fresh on every call: a set edited a moment ago must not leave a stale target allowed.
        var linked = await linkedBoardSetRepository.GetLinkedBoardsForViewerAsync(sourceGroupId, actorUserId, token);
        var sourceName = (await linkedBoardSetRepository.GetBoardAsync(sourceGroupId, token))?.Name ?? string.Empty;

        var preview = new QuestMovePreview
        {
            EntryId = quest.Id,
            EntryTitle = quest.Title,
            SourceGroupId = sourceGroupId,
            SourceBoardName = sourceName,
            LinkedBoards = linked.Boards,
            TargetBoardType = linked.SetBoardType,
            Quest = quest,
            DmName = quest.DungeonMaster?.Name ?? "Unknown DM",
            DungeonMasterId = quest.DungeonMasterId
        };

        if (linked.Boards.Count == 0)
        {
            preview.Status = MoveStatus.NoLinkedBoards;
            return preview;
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
                quest.Id, sourceGroupId, target.Id);
            outcome = MoveStatus.Failed;
        }

        if (outcome == MoveStatus.Moved)
        {
            logger.LogInformation(
                "Moved quest {QuestId} from board {SourceGroupId} to board {TargetGroupId} by user {ActorUserId}: kept {Kept}, dropped {Dropped}, promoted {Promoted}, links cut {LinksCut}",
                quest.Id, sourceGroupId, target.Id, actorUserId,
                plan.KeptSignupIds.Count, plan.DroppedSignupIds.Count, plan.PromotedSignupIds.Count,
                (plan.ClearOriginalLink ? 1 : 0) + plan.FollowUpQuestIdsToUnlink.Count);
        }

        preview.Status = outcome;
        return preview;
    }
}
