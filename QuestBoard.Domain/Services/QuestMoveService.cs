using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Domain.Services;

internal class QuestMoveService(
    IQuestRepository questRepository,
    IActiveGroupContext activeGroupContext,
    ILinkedBoardSetRepository linkedBoardSetRepository,
    IQuestMoveRepository moveRepository,
    IBoardClock boardClock,
    IUserRepository userRepository,
    IIdentityService identityService,
    IQuestEmailDispatcher dispatcher,
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

            // Only after the save: the move is committed, so mail trouble can never undo it.
            EnqueueMoveMail(preview, actorUserId);
        }

        preview.Status = outcome;
        return preview;
    }

    // Reads only the snapshot taken before the move. Each enqueue stands alone because the move is
    // already committed: a failed enqueue is logged and the others still go out.
    private void EnqueueMoveMail(QuestMovePreview preview, int actorUserId)
    {
        if (preview.Quest is not { } quest || preview.Plan is not { } plan || preview.Target is not { } target)
        {
            return;
        }

        var dmName = quest.DungeonMaster?.Name ?? "Unknown DM";
        var sessionDate = quest.IsFinalized ? quest.FinalizedDate : null;
        var mailed = new HashSet<int>();

        bool TryClaim(User? user)
        {
            if (user == null || string.IsNullOrEmpty(user.Email) || !user.EmailConfirmed || user.Id == actorUserId)
            {
                return false;
            }

            return mailed.Add(user.Id);
        }

        void Send(Action enqueue)
        {
            try
            {
                enqueue();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not enqueue move mail for quest {QuestId}", quest.Id);
            }
        }

        if (TryClaim(quest.DungeonMaster))
        {
            var dm = quest.DungeonMaster!;
            Send(() => dispatcher.EnqueueQuestMovedEmail(
                quest.Id, target.Id, QuestMovedAudience.DungeonMaster, dm.Email!, quest.Title, dmName, target.Name, sessionDate));
        }

        var promotedIds = plan.PromotedSignupIds.ToHashSet();
        var keptIds = plan.KeptSignupIds.ToHashSet();
        var droppedIds = plan.DroppedSignupIds.ToHashSet();

        foreach (var signup in quest.PlayerSignups.Where(s => keptIds.Contains(s.Id) && !promotedIds.Contains(s.Id)))
        {
            if (TryClaim(signup.Player))
            {
                Send(() => dispatcher.EnqueueQuestMovedEmail(
                    quest.Id, target.Id, QuestMovedAudience.Player, signup.Player.Email!, quest.Title, dmName, target.Name, sessionDate));
            }
        }

        foreach (var signup in quest.PlayerSignups.Where(s => droppedIds.Contains(s.Id)))
        {
            if (TryClaim(signup.Player))
            {
                Send(() => dispatcher.EnqueueQuestMovedEmail(
                    quest.Id, target.Id, QuestMovedAudience.Removed, signup.Player.Email!, quest.Title, dmName, target.Name, sessionDate));
            }
        }

        foreach (var signup in quest.PlayerSignups.Where(s => promotedIds.Contains(s.Id)))
        {
            if (quest.FinalizedDate is { } finalizedDate && TryClaim(signup.Player))
            {
                Send(() => dispatcher.EnqueueWaitlistPromotedAfterMoveEmail(
                    quest.Id, target.Id, finalizedDate, signup.Player.Email!, signup.Player.Name, quest.Title,
                    dmName, quest.Description, quest.ChallengeRating, target.Name));
            }
        }
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

        // Read from the stored role on the target, not from the viewer's own role there: the DM is
        // a different person from the actor, and a platform super admin runs a quest anywhere.
        var dmRole = await userRepository.GetGroupRoleAsync(quest.DungeonMasterId, target.Id);
        var dmCanRun = dmRole is GroupRole.DungeonMaster or GroupRole.Admin
            || await identityService.IsSuperAdminAsync(quest.DungeonMasterId);
        if (!dmCanRun)
        {
            preview.Status = MoveStatus.DungeonMasterNotEligible;
            return preview;
        }

        // Membership means any role on the target board, and needs no filter bypass.
        var memberIds = (await userRepository.GetAllGroupMembers(target.Id, token)).Select(u => u.Id).ToHashSet();
        var kept = quest.PlayerSignups.Where(s => memberIds.Contains(s.Player.Id)).ToList();
        var dropped = quest.PlayerSignups.Where(s => !memberIds.Contains(s.Player.Id)).ToList();

        preview.Dropped = dropped
            .OrderByDescending(s => s.IsSelected)
            .ThenBy(s => s.Player.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new MovePerson(s.Player.Id, s.Player.Name, s.Role, s.IsSelected))
            .ToList();
        preview.CharactersCleared = kept.Count(s => s.CharacterId != null);
        preview.SeatsOpened = dropped.Count(s => s.IsSelected && s.Role == SignupRole.Player);

        // A seat is only refilled on a finalized quest whose date matches a proposed date; an open
        // quest has no date to rank the waitlist by, so its seats stay open.
        var finalizedDate = quest.IsFinalized ? quest.FinalizedDate : null;
        var finalizedProposedDate = finalizedDate == null
            ? null
            : quest.ProposedDates.FirstOrDefault(pd => pd.Date.Date == finalizedDate.Value.Date);
        var promoted = finalizedProposedDate == null
            ? []
            : kept.SelectPromotions(finalizedProposedDate.Id, preview.SeatsOpened).ToList();
        preview.PromotedCount = promoted.Count;

        // Chain links that would point across boards are cut at both ends.
        var followUpIds = quest.FollowUpQuest is { } followUp ? new List<int> { followUp.Id } : [];
        preview.OriginalQuestTitle = quest.OriginalQuest?.Title;
        preview.FollowUpQuestTitle = quest.FollowUpQuest?.Title;

        var plan = new QuestMovePlan(
            quest.Id,
            sourceGroupId,
            target.Id,
            kept.Select(s => s.Id).ToList(),
            dropped.Select(s => s.Id).ToList(),
            promoted.Select(s => s.Id).ToList(),
            ClearOriginalLink: quest.OriginalQuestId != null,
            FollowUpQuestIdsToUnlink: followUpIds);
        preview.Plan = plan;
        preview.KeptCount = kept.Count;

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
