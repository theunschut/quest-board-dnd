using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Interfaces;

public interface IQuestRepository : IBaseRepository<Quest>
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
    /// Marks a quest as finalized on the given date and records which player signups were selected.
    /// Spectator signups are auto-selected regardless of the selected ID list.
    /// </summary>
    Task FinalizeQuestAsync(int questId, DateTime finalizedDate, IList<int> selectedPlayerSignupIds, CancellationToken token = default);

    /// <summary>
    /// Reopens a finalized quest, clearing its finalized date and deselecting all player signups.
    /// </summary>
    Task OpenQuestAsync(int questId, CancellationToken token = default);

    /// <summary>
    /// Updates a quest's editable properties and, when requested, reconciles its proposed dates.
    /// Returns the distinct players whose date votes were removed as a result, so the caller can notify them.
    /// </summary>
    Task<IList<User>> UpdateQuestPropertiesWithNotificationsAsync(int questId, string title, string description, int challengeRating, int totalPlayerCount, bool dungeonMasterSession, bool updateProposedDates = false, IList<DateTime>? proposedDates = null, CancellationToken token = default);

    /// <summary>
    /// Sets the recap text for a finalized quest.
    /// </summary>
    Task UpdateQuestRecapAsync(int questId, string recap, CancellationToken token = default);

    /// <summary>
    /// Records the date for which the finalized-quest email has already been sent, to prevent duplicate sends.
    /// </summary>
    Task SetFinalizedEmailSentForDateAsync(int questId, DateTime date, CancellationToken token = default);

    /// <summary>
    /// Returns whether a follow-up quest already exists for the given original quest.
    /// </summary>
    Task<bool> HasFollowUpQuestAsync(int questId, CancellationToken token = default);

    /// <summary>
    /// Returns all quests run by the given DM, most recently finalized (or created) first.
    /// </summary>
    Task<IList<Quest>> GetQuestsByDungeonMasterAsync(int dmUserId, CancellationToken token = default);

    /// <summary>
    /// Returns finalized quests whose finalized date matches the given date, scoped to the active group.
    /// </summary>
    Task<IList<Quest>> GetFinalizedQuestsForDateAsync(DateTime date, CancellationToken token = default);

    /// <summary>
    /// Returns all finalized quests for the given date across ALL groups.
    /// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob).
    /// </summary>
    Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);
}
