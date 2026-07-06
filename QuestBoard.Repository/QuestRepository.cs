using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class QuestRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<Quest, QuestEntity>(dbContext, mapper), IQuestRepository
{
    // Tolerance window for treating two proposed dates as the same slot;
    // accommodates minor timezone rounding when users resubmit dates.
    private const int DateMatchWindowMinutes = 30;


    /// <inheritdoc/>
    public override async Task AddAsync(Quest model, CancellationToken token = default)
    {
        try
        {
            await base.AddAsync(model, token);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Quests_OriginalQuestId") == true)
        {
            throw new InvalidOperationException("A follow-up quest already exists for this quest.", ex);
        }
    }

    /// <inheritdoc/>
    public override async Task<IList<Quest>> GetAllAsync(CancellationToken token = default)
    {
        var entities = await DbContext.Quests
            .Include(q => q.DungeonMaster)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsWithDetailsAsync(CancellationToken token = default)
    {
        var entities = await ProjectWithoutCharacterImages(DbContext.Quests)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsForCalendarAsync(CancellationToken token = default)
    {
        var entities = await ProjectForCalendar(DbContext.Quests)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsWithSignupsAsync(CancellationToken token = default)
    {
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        var entities = await ProjectWithoutCharacterImages(DbContext.Quests)
            .Where(q => (!q.IsFinalized || (q.IsFinalized && q.FinalizedDate > oneDayAgo)) && !q.IsClosed)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsWithSignupsForRoleAsync(bool isAdminOrDm, CancellationToken token = default)
    {
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        var entities = await ProjectWithoutCharacterImages(DbContext.Quests)
            .Where(q => (!q.IsFinalized || (q.IsFinalized && q.FinalizedDate > oneDayAgo)) &&
                        (!q.DungeonMasterSession || isAdminOrDm) && !q.IsClosed)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<Quest?> GetQuestWithDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await ProjectWithoutCharacterImages(DbContext.Quests)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken: token);
        return entity == null ? null : Mapper.Map<Quest>(entity);
    }

    /// <inheritdoc/>
    public async Task<Quest?> GetQuestWithManageDetailsAsync(int id, CancellationToken token = default)
    {
        // Two independent collection Includes (ProposedDates and PlayerSignups) in a single
        // query force EF to cross-join both collections, multiplying row count combinatorially
        // and triggering the MultipleCollectionIncludeWarning. AsSplitQuery() issues one query
        // per collection instead, avoiding the row-count blowup without changing the loaded shape.
        var entity = await DbContext.Quests
            .AsSplitQuery()
            .Include(q => q.ProposedDates)
                .ThenInclude(pd => pd.PlayerVotes)
                    .ThenInclude(pv => pv.PlayerSignup)
                        .ThenInclude(ps => ps!.Player)
            .Include(q => q.PlayerSignups)
                .ThenInclude(ps => ps.Player)
            .Include(q => q.DungeonMaster)
            .Include(q => q.OriginalQuest)
            .Include(q => q.FollowUpQuest)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken: token);
        return entity == null ? null : Mapper.Map<Quest>(entity);
    }

    /// <inheritdoc/>
    public async Task<Quest?> GetQuestWithManageViewDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await ProjectWithoutCharacterImages(DbContext.Quests)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken: token);
        return entity == null ? null : Mapper.Map<Quest>(entity);
    }

    /// <inheritdoc/>
    public async Task FinalizeQuestAsync(int questId, DateTime finalizedDate, IList<int> selectedPlayerSignupIds, CancellationToken token = default)
    {
        var entity = await DbContext.Quests
            .Include(q => q.PlayerSignups)
            .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
        if (entity == null) return;

        entity.IsFinalized = true;
        entity.FinalizedDate = finalizedDate;

        foreach (var playerSignup in entity.PlayerSignups)
        {
            // Auto-approve spectators
            if ((SignupRole)playerSignup.SignupRole == SignupRole.Spectator)
            {
                playerSignup.IsSelected = true;
            }
            else
            {
                playerSignup.IsSelected = selectedPlayerSignupIds.Contains(playerSignup.Id);
            }
        }

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task OpenQuestAsync(int questId, CancellationToken token = default)
    {
        var entity = await DbContext.Quests
            .Include(q => q.PlayerSignups)
            .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
        if (entity == null) return;

        entity.IsFinalized = false;
        entity.FinalizedDate = null;

        foreach (var playerSignup in entity.PlayerSignups)
        {
            playerSignup.IsSelected = false;
        }

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task CloseQuestAsync(int questId, CancellationToken token = default)
    {
        var entity = await DbContext.Quests.FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
        if (entity == null) return;

        entity.IsClosed = true;
        entity.ClosedDate = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task ReopenQuestAsync(int questId, CancellationToken token = default)
    {
        var entity = await DbContext.Quests.FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
        if (entity == null) return;

        entity.IsClosed = false;
        entity.ClosedDate = null;

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> UpdateQuestPropertiesWithNotificationsAsync(int questId, string title, string description, string? rewards, int challengeRating, int totalPlayerCount, bool dungeonMasterSession, bool updateProposedDates = false, IList<DateTime>? proposedDates = null, CancellationToken token = default)
    {
        var entity = await DbContext.Quests
            .Include(q => q.ProposedDates)
                .ThenInclude(pd => pd.PlayerVotes)
                    .ThenInclude(pv => pv.PlayerSignup)
                        .ThenInclude(ps => ps!.Player)
            .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
        if (entity == null) return [];

        var affectedPlayerEntities = new List<UserEntity>();

        entity.Title = title;
        entity.Description = description;
        entity.Rewards = rewards;
        entity.ChallengeRating = challengeRating;
        entity.TotalPlayerCount = totalPlayerCount;
        entity.DungeonMasterSession = dungeonMasterSession;

        if (updateProposedDates && proposedDates != null)
        {
            affectedPlayerEntities = UpdateProposedDatesWithNotificationTracking(entity, proposedDates);
        }

        await DbContext.SaveChangesAsync(token);

        var affectedPlayers = Mapper.Map<IList<User>>(affectedPlayerEntities);
        return affectedPlayers.GroupBy(p => p.Id).Select(g => g.First()).ToList();
    }

    /// <inheritdoc/>
    public async Task UpdateQuestRecapAsync(int questId, string recap, CancellationToken token = default)
    {
        var entity = await DbContext.Quests.FindAsync([questId], cancellationToken: token);
        if (entity == null) return;

        entity.Recap = recap;
        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task SetFinalizedEmailSentForDateAsync(int questId, DateTime date, CancellationToken token = default)
    {
        var entity = await DbContext.Quests.FindAsync([questId], cancellationToken: token);
        if (entity == null) return;

        entity.FinalizedEmailSentForDate = date;
        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> HasFollowUpQuestAsync(int questId, CancellationToken token = default)
    {
        return await DbContext.Quests.AnyAsync(q => q.OriginalQuestId == questId, token);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsByDungeonMasterAsync(int dmUserId, CancellationToken token = default)
    {
        var entities = await DbContext.Quests
            .Include(q => q.DungeonMaster)
            .Where(q => q.DungeonMasterId == dmUserId)
            .OrderByDescending(q => q.FinalizedDate ?? q.CreatedAt)
            .ToListAsync(token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetFinalizedQuestsForDateAsync(DateTime date, CancellationToken token = default)
    {
        var entities = await ProjectWithoutCharacterImages(DbContext.Quests)
            .Where(q => q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date == date.Date)
            .ToListAsync(token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default)
    {
        // Explicit cross-group intent — IgnoreQueryFilters bypasses HasQueryFilter on QuestEntity
        var entities = await ProjectWithoutCharacterImages(DbContext.Quests.IgnoreQueryFilters())
            .Where(q => q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date == date.Date)
            .ToListAsync(token);
        return Mapper.Map<IList<Quest>>(entities);
    }

    private static bool IsSameDateTime(DateTime date1, DateTime date2)
    {
        return Math.Abs((date1 - date2).TotalMinutes) <= DateMatchWindowMinutes;
    }

    private static void UpdateProposedDatesIntelligently(QuestEntity entity, IList<DateTime> newProposedDates)
    {
        var existingDates = entity.ProposedDates.ToList();
        var datesToRemove = new List<ProposedDateEntity>();

        foreach (var existingDate in existingDates)
        {
            var matchingNewDate = newProposedDates.FirstOrDefault(nd => IsSameDateTime(existingDate.Date, nd));
            if (matchingNewDate == default)
            {
                datesToRemove.Add(existingDate);
            }
            else
            {
                existingDate.Date = matchingNewDate;
            }
        }

        foreach (var newDate in newProposedDates)
        {
            if (!existingDates.Any(ed => IsSameDateTime(ed.Date, newDate)))
            {
                entity.ProposedDates.Add(new ProposedDateEntity { Date = newDate, Quest = entity, QuestId = entity.Id });
            }
        }

        foreach (var dateToRemove in datesToRemove)
        {
            entity.ProposedDates.Remove(dateToRemove);
        }
    }

    private static List<UserEntity> UpdateProposedDatesWithNotificationTracking(QuestEntity entity, IList<DateTime> newProposedDates)
    {
        var existingDates = entity.ProposedDates.ToList();
        var datesToRemove = new List<ProposedDateEntity>();
        var affectedPlayerEntities = new List<UserEntity>();

        foreach (var existingDate in existingDates)
        {
            var matchingNewDate = newProposedDates.FirstOrDefault(nd => IsSameDateTime(existingDate.Date, nd));
            if (matchingNewDate == default)
            {
                datesToRemove.Add(existingDate);
                if (existingDate.PlayerVotes?.Count > 0)
                {
                    affectedPlayerEntities.AddRange(
                        existingDate.PlayerVotes
                            .Where(pv => pv.PlayerSignup?.Player != null)
                            .Select(pv => pv.PlayerSignup!.Player!));
                }
            }
            else
            {
                existingDate.Date = matchingNewDate;
            }
        }

        foreach (var newDate in newProposedDates)
        {
            if (!existingDates.Any(ed => IsSameDateTime(ed.Date, newDate)))
            {
                entity.ProposedDates.Add(new ProposedDateEntity { Date = newDate, Quest = entity, QuestId = entity.Id });
            }
        }

        foreach (var dateToRemove in datesToRemove)
        {
            entity.ProposedDates.Remove(dateToRemove);
        }

        return affectedPlayerEntities;
    }

    private static IQueryable<QuestEntity> ProjectForCalendar(IQueryable<QuestEntity> query)
    {
        return query
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.DungeonMaster)
            .Include(q => q.PlayerSignups)
                .ThenInclude(ps => ps.Player)
            .Include(q => q.ProposedDates)
                .ThenInclude(pd => pd.PlayerVotes)
                    .ThenInclude(pv => pv.PlayerSignup)
                        .ThenInclude(ps => ps!.Player);
    }

    private static IQueryable<QuestEntity> ProjectWithoutCharacterImages(IQueryable<QuestEntity> query)
    {
        return query
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.ProposedDates)
                .ThenInclude(pd => pd.PlayerVotes)
                    .ThenInclude(pv => pv.PlayerSignup)
                        .ThenInclude(ps => ps!.Player)
            .Include(q => q.PlayerSignups)
                .ThenInclude(ps => ps.Player)
            .Include(q => q.PlayerSignups)
                .ThenInclude(ps => ps.DateVotes)
            .Include(q => q.PlayerSignups)
                .ThenInclude(ps => ps.Character)
                    .ThenInclude(c => c!.Classes)
            .Include(q => q.DungeonMaster)
            .Include(q => q.OriginalQuest)
            .Include(q => q.FollowUpQuest);
    }
}
