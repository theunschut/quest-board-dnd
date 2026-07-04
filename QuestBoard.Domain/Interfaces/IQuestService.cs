using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Interfaces;

public interface IQuestService : IBaseService<Quest>
{
    /// <summary>
    /// Returns all quests with proposed dates, signups, and player details loaded, scoped to the active group.
    /// </summary>
    Task<IList<Quest>> GetQuestsWithDetailsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all quests projected for the monthly calendar view, including signups, votes, and DM details.
    /// </summary>
    Task<IList<Quest>> GetQuestsForCalendarAsync(CancellationToken token = default);

    /// <summary>
    /// Returns open quests and quests finalized within the last day, ordered newest first.
    /// </summary>
    Task<IList<Quest>> GetQuestsWithSignupsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns open/recently-finalized quests, additionally filtering out DM-only sessions for non-Admin/DM callers.
    /// </summary>
    Task<IList<Quest>> GetQuestsWithSignupsForRoleAsync(bool isAdminOrDm, CancellationToken token = default);

    /// <summary>
    /// Returns a single quest with proposed dates, signups, and player details loaded.
    /// </summary>
    Task<Quest?> GetQuestWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns a single quest with the full management detail graph loaded (dates, votes, signups, DM, original/follow-up quest links).
    /// </summary>
    Task<Quest?> GetQuestWithManageDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns a single quest with the same detail graph as GetQuestsWithDetailsAsync, for the manage-view page.
    /// </summary>
    Task<Quest?> GetQuestWithManageViewDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Updates a quest's editable properties and, when requested, reconciles its proposed dates.
    /// If any players' date votes were removed, enqueues a date-changed email to those players.
    /// </summary>
    Task<ServiceResult<int>> UpdateQuestPropertiesWithNotificationsAsync(int questId, string title, string description, int challengeRating, int totalPlayerCount, bool dungeonMasterSession, bool updateProposedDates = false, IList<DateTime>? proposedDates = null, CancellationToken token = default);

    /// <summary>
    /// Finalizes a quest for the given date and selected player signups, then enqueues a finalized-quest email to the selected players.
    /// </summary>
    Task FinalizeQuestAsync(int questId, DateTime finalizedDate, IList<int> selectedPlayerSignupIds, CancellationToken token = default);

    /// <summary>
    /// Reopens a finalized quest, clearing its finalized date and deselecting all player signups.
    /// </summary>
    Task OpenQuestAsync(int questId, CancellationToken token = default);

    /// <summary>
    /// Closes a campaign quest, setting IsClosed and recording the close date. Sends no email.
    /// </summary>
    Task CloseQuestAsync(int questId, CancellationToken token = default);

    /// <summary>
    /// Reopens a closed campaign quest, clearing IsClosed and its close date. Sends no email.
    /// </summary>
    Task ReopenQuestAsync(int questId, CancellationToken token = default);

    /// <summary>
    /// Returns finalized, non-DM-session quests whose finalized date is at least one day in the past.
    /// </summary>
    Task<IList<Quest>> GetCompletedQuestsAsync(CancellationToken token = default);

    /// <summary>
    /// Sets the recap text for a finalized quest.
    /// </summary>
    Task UpdateQuestRecapAsync(int questId, string recap, CancellationToken token = default);

    /// <summary>
    /// Creates a follow-up quest from a finalized original quest.
    /// Copies Title+" - Part 2", Description, ChallengeRating, TotalPlayerCount, DungeonMasterId.
    /// Clears ProposedDates. Resets DungeonMasterSession to false.
    /// Bulk-imports IsSelected=true signups from original as SignupRole.Player.
    /// Returns the Id of the newly created follow-up quest.
    /// </summary>
    Task<int> CreateFollowUpQuestAsync(int originalQuestId, CancellationToken token = default);

    /// <summary>
    /// Creates a follow-up quest from a finalized original quest, then applies the given
    /// title, description, challenge rating, player count, DM-session flag, and proposed dates.
    /// Imports IsSelected=true signups from the original quest as SignupRole.Player.
    /// If applying the details fails, the newly created follow-up shell is removed before the
    /// original exception is re-thrown, so no orphaned quest is left behind.
    /// Returns the Id of the newly created follow-up quest.
    /// </summary>
    Task<int> CreateFollowUpQuestWithDetailsAsync(int originalQuestId, string title, string description, int challengeRating, int totalPlayerCount, bool dungeonMasterSession, IList<DateTime> proposedDates, CancellationToken token = default);

    /// <summary>
    /// Returns all quests where DungeonMasterId == dmUserId, ordered by most recent first.
    /// Includes both finalized and active quests.
    /// </summary>
    Task<IList<Quest>> GetQuestsByDungeonMasterAsync(int dmUserId, CancellationToken token = default);

    /// <summary>
    /// Records a player's vote change on a finalized quest. A Yes vote selects the player only
    /// when a fresh server-side seat count shows room (never rejects on capacity). A selected
    /// player voting No frees their seat and triggers promotion of the top waitlisted candidate.
    /// A Maybe vote never changes selection state and never triggers promotion.
    /// </summary>
    Task ChangeVoteAsync(int questId, int playerSignupId, VoteType vote, int finalizedProposedDateId, CancellationToken token = default);

    /// <summary>
    /// Deletes a player's signup on a finalized quest. If the deleted signup was selected,
    /// triggers promotion of the top waitlisted candidate into the freed seat. A waitlisted
    /// (non-selected) player revoking their signup triggers no promotion.
    /// </summary>
    Task RevokeSignupAsync(int questId, int playerSignupId, CancellationToken token = default);
}