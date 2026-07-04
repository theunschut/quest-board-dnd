using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Services;

internal class QuestService(
    IQuestRepository repository,
    IPlayerSignupRepository playerSignupRepository,
    IQuestEmailDispatcher dispatcher,
    IMapper mapper) : BaseService<Quest>(repository, mapper), IQuestService
{
    /// <inheritdoc/>
    public async Task FinalizeQuestAsync(int questId, DateTime finalizedDate, IList<int> selectedPlayerSignupIds, CancellationToken token = default)
    {
        await repository.FinalizeQuestAsync(questId, finalizedDate, selectedPlayerSignupIds, token);

        // Re-fetch post-save to avoid stale IsSelected state
        var quest = await repository.GetQuestWithDetailsAsync(questId, token);
        if (quest == null) return;

        var selectedSignups = quest.PlayerSignups
            .Where(ps => (selectedPlayerSignupIds.Contains(ps.Id) || ps.Role == SignupRole.Spectator)
                         && !string.IsNullOrEmpty(ps.Player.Email)
                         && ps.Player.EmailConfirmed)
            .ToList();

        if (selectedSignups.Count == 0) return;

        var recipientEmails = selectedSignups.Select(s => s.Player.Email!).ToArray();
        var playerNames     = selectedSignups.Select(s => s.Player.Name).ToArray();

        dispatcher.EnqueueFinalizedEmail(
            quest.Id,
            quest.GroupId,    // group context for Hangfire job filter
            finalizedDate,
            recipientEmails,
            playerNames,
            quest.Title,
            quest.DungeonMaster?.Name ?? "Unknown DM",
            quest.Description,
            quest.ChallengeRating);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsWithDetailsAsync(CancellationToken token = default)
    {
        return await repository.GetQuestsWithDetailsAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsForCalendarAsync(CancellationToken token = default)
    {
        return await repository.GetQuestsForCalendarAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsWithSignupsAsync(CancellationToken token = default)
    {
        return await repository.GetQuestsWithSignupsAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsWithSignupsForRoleAsync(bool isAdminOrDm, CancellationToken token = default)
    {
        return await repository.GetQuestsWithSignupsForRoleAsync(isAdminOrDm, token);
    }

    /// <inheritdoc/>
    public async Task<Quest?> GetQuestWithDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetQuestWithDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task<Quest?> GetQuestWithManageDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetQuestWithManageDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task<Quest?> GetQuestWithManageViewDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetQuestWithManageViewDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task OpenQuestAsync(int questId, CancellationToken token = default)
    {
        await repository.OpenQuestAsync(questId, token);
    }

    /// <inheritdoc/>
    public async Task CloseQuestAsync(int questId, CancellationToken token = default)
    {
        await repository.CloseQuestAsync(questId, token);
    }

    /// <inheritdoc/>
    public async Task ReopenQuestAsync(int questId, CancellationToken token = default)
    {
        await repository.ReopenQuestAsync(questId, token);
    }

    /// <inheritdoc/>
    public override async Task RemoveAsync(Quest model, CancellationToken token = default)
    {
        var quest = await repository.GetQuestWithManageDetailsAsync(model.Id, token);
        if (quest == null) return;

        // Manual cleanup required since Quest->PlayerSignup is NoAction to avoid cascade cycles
        // Remove PlayerSignups first (DateVotes will cascade delete from PlayerSignups)
        var playerSignupsToRemove = quest.PlayerSignups?.ToList() ?? [];
        foreach (var playerSignup in playerSignupsToRemove)
        {
            await playerSignupRepository.RemoveAsync(playerSignup, token);
        }

        // ProposedDates will cascade delete automatically when Quest is removed
        await repository.RemoveAsync(quest, token);
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(Quest model, CancellationToken token = default)
    {
        await repository.UpdateAsync(model, token);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<int>> UpdateQuestPropertiesWithNotificationsAsync(
        int questId, string title, string description, int challengeRating, int totalPlayerCount,
        bool dungeonMasterSession, bool updateProposedDates = false, IList<DateTime>? proposedDates = null,
        CancellationToken token = default)
    {
        // Capture old proposed dates before the update so we can report what changed in the email
        DateTime oldDate = default;
        DateTime newDate = default;
        if (updateProposedDates && proposedDates is { Count: > 0 })
        {
            var questBefore = await repository.GetQuestWithDetailsAsync(questId, token);
            if (questBefore?.ProposedDates is { Count: > 0 })
            {
                oldDate = questBefore.ProposedDates.OrderBy(d => d.Date).First().Date;
            }
            newDate = proposedDates.OrderBy(d => d).First();
        }

        var affectedPlayers = await repository.UpdateQuestPropertiesWithNotificationsAsync(
            questId, title, description, challengeRating, totalPlayerCount, dungeonMasterSession,
            updateProposedDates, proposedDates, token);

        if (affectedPlayers.Count == 0) return ServiceResult<int>.Ok(0);

        var quest = await repository.GetQuestWithDetailsAsync(questId, token);
        if (quest == null) return ServiceResult<int>.Ok(0);

        var withEmail = affectedPlayers.WhereEmailConfirmed().Where(p => !string.IsNullOrEmpty(p.Email)).ToList();
        if (withEmail.Count == 0) return ServiceResult<int>.Ok(0);

        dispatcher.EnqueueDateChangedEmail(
            questId,
            withEmail.Select(p => p.Email!).ToArray(),
            withEmail.Select(p => p.Name).ToArray(),
            quest.Title,
            quest.DungeonMaster?.Name ?? "Unknown DM",
            oldDate,
            newDate);

        return ServiceResult<int>.Ok(withEmail.Count);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetCompletedQuestsAsync(CancellationToken token = default)
    {
        var quests = await repository.GetQuestsWithDetailsAsync(token);

        return quests
            .Where(q => (q.IsFinalized
                         && q.FinalizedDate.HasValue
                         && q.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
                         && !q.DungeonMasterSession)
                        || (q.IsClosed && !q.DungeonMasterSession))
            .OrderByDescending(q => q.IsClosed ? q.ClosedDate : q.FinalizedDate)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task UpdateQuestRecapAsync(int questId, string recap, CancellationToken token = default)
    {
        await repository.UpdateQuestRecapAsync(questId, recap, token);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsByDungeonMasterAsync(int dmUserId, CancellationToken token = default)
    {
        return await repository.GetQuestsByDungeonMasterAsync(dmUserId, token);
    }

    /// <inheritdoc/>
    public async Task<int> CreateFollowUpQuestAsync(int originalQuestId, CancellationToken token = default)
    {
        var original = await repository.GetQuestWithDetailsAsync(originalQuestId, token);
        if (original == null)
            throw new InvalidOperationException($"Quest {originalQuestId} not found.");

        // Guard: original quest must be finalized before a follow-up can be created
        if (!original.IsFinalized)
            throw new InvalidOperationException("Cannot create a follow-up for a quest that has not been finalized.");

        // Enforce at most one direct follow-up (checked via repository to avoid nav property load issues)
        if (await repository.HasFollowUpQuestAsync(originalQuestId, token))
            throw new InvalidOperationException("A follow-up quest already exists for this quest.");

        // Copy fields, append title, clear dates, reset DM session
        var followUp = new Quest
        {
            Title = $"{original.Title} - Part 2",
            Description = original.Description,
            ChallengeRating = original.ChallengeRating,
            TotalPlayerCount = original.TotalPlayerCount,
            DungeonMasterId = original.DungeonMasterId,
            DungeonMasterSession = false,
            GroupId = original.GroupId,
            ProposedDates = [],
            OriginalQuestId = original.Id,
        };

        // Persist the quest first to obtain its Id
        await repository.AddAsync(followUp, token);

        // Import IsSelected=true players as SignupRole.Player immediately on save
        var selectedSignups = original.PlayerSignups
            .Where(ps => ps.IsSelected)
            .ToList();

        foreach (var ps in selectedSignups)
        {
            var importedSignup = new PlayerSignup
            {
                Player = ps.Player,
                Quest = followUp,
                Role = SignupRole.Player,    // always Player regardless of original role
                IsSelected = true,
                SignupTime = DateTime.UtcNow,
                DateVotes = [],
            };
            await playerSignupRepository.AddAsync(importedSignup, token);
        }

        return followUp.Id;
    }

    /// <inheritdoc/>
    public async Task ChangeVoteAsync(int questId, int playerSignupId, VoteType vote, int finalizedProposedDateId, CancellationToken token = default)
    {
        var seatFreed = await playerSignupRepository.ChangeVoteAsync(playerSignupId, finalizedProposedDateId, vote, token);

        if (vote == VoteType.Yes || vote == VoteType.Maybe)
        {
            // A Yes or Maybe vote can both fill an open seat when one is available; only a No
            // vote never grants a seat.
            // Re-fetch post-vote to avoid stale IsSelected/seat-count state; never trust a
            // client-supplied capacity signal for the selection decision.
            var quest = await repository.GetQuestWithDetailsAsync(questId, token);
            if (quest != null)
            {
                var signup = quest.PlayerSignups.FirstOrDefault(ps => ps.Id == playerSignupId);
                if (signup != null && !signup.IsSelected)
                {
                    var selectedCount = quest.PlayerSignups.Count(ps => ps.IsSelected && ps.Role == SignupRole.Player);
                    if (selectedCount < quest.TotalPlayerCount)
                    {
                        signup.IsSelected = true;
                        await playerSignupRepository.UpdateAsync(signup, token);
                    }
                }
            }
        }

        if (seatFreed)
        {
            await PromoteNextWaitlistedPlayerIfSeatFreedAsync(questId, finalizedProposedDateId, freeingPlayerSignupId: playerSignupId, token);
        }
    }

    /// <inheritdoc/>
    public async Task RevokeSignupAsync(int questId, int playerSignupId, CancellationToken token = default)
    {
        var quest = await repository.GetQuestWithDetailsAsync(questId, token);
        if (quest == null) return;

        var signup = quest.PlayerSignups.FirstOrDefault(ps => ps.Id == playerSignupId);
        if (signup == null) return;

        var wasSelected = signup.IsSelected;

        await playerSignupRepository.RemoveAsync(signup, token);

        if (!wasSelected) return;

        var finalizedProposedDate = quest.ProposedDates
            .FirstOrDefault(pd => quest.FinalizedDate.HasValue && pd.Date.Date == quest.FinalizedDate.Value.Date);
        if (finalizedProposedDate == null) return;

        await PromoteNextWaitlistedPlayerIfSeatFreedAsync(questId, finalizedProposedDate.Id, freeingPlayerSignupId: playerSignupId, token);
    }

    /// <summary>
    /// Finds the top waitlisted candidate for the given quest/date and promotes them into the
    /// freed seat, emailing only that one candidate. Never promotes the player who freed the seat.
    /// </summary>
    private async Task PromoteNextWaitlistedPlayerIfSeatFreedAsync(int questId, int finalizedProposedDateId, int freeingPlayerSignupId, CancellationToken token)
    {
        var candidate = await playerSignupRepository.GetTopWaitlistedCandidateAsync(questId, finalizedProposedDateId, token);
        if (candidate == null) return;

        // The freeing player must never be the one promoted.
        if (candidate.Id == freeingPlayerSignupId) return;

        candidate.IsSelected = true;
        await playerSignupRepository.UpdateAsync(candidate, token);

        // Selection is never gated on email eligibility — only the email send is.
        if (string.IsNullOrEmpty(candidate.Player.Email) || !candidate.Player.EmailConfirmed) return;

        var quest = await repository.GetQuestWithDetailsAsync(questId, token);
        if (quest == null || quest.FinalizedDate == null) return;

        dispatcher.EnqueueWaitlistPromotedEmail(
            quest.Id,
            quest.GroupId,
            quest.FinalizedDate.Value,
            candidate.Player.Email!,
            candidate.Player.Name,
            quest.Title,
            quest.DungeonMaster?.Name ?? "Unknown DM",
            quest.Description,
            quest.ChallengeRating);
    }

    /// <inheritdoc/>
    public async Task<int> CreateFollowUpQuestWithDetailsAsync(
        int originalQuestId, string title, string description, int challengeRating, int totalPlayerCount,
        bool dungeonMasterSession, IList<DateTime> proposedDates, CancellationToken token = default)
    {
        // Create the shell quest and import selected players first
        var newQuestId = await CreateFollowUpQuestAsync(originalQuestId, token);

        try
        {
            // Apply the proposed dates and title/description edits from the form
            // (CreateFollowUpQuestAsync creates the quest shell without dates; dates come from the form)
            await UpdateQuestPropertiesWithNotificationsAsync(
                newQuestId, title, description, challengeRating, totalPlayerCount,
                dungeonMasterSession, updateProposedDates: true, proposedDates, token);
        }
        catch
        {
            // Roll back the shell quest so the unique FK is freed and retries are possible.
            // A failure during rollback must never mask the original exception below.
            try
            {
                var orphan = await GetQuestWithDetailsAsync(newQuestId, token);
                if (orphan != null)
                    await RemoveAsync(orphan, token);
            }
            catch
            {
                // Swallow rollback failures - the primary exception is what the caller needs to see.
            }

            throw;
        }

        return newQuestId;
    }
}
