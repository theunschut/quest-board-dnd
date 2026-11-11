using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper, IBoardClock boardClock)
    : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository
{
    /// <inheritdoc/>
    public async Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default)
    {
        return await DbContext.Groups
            .Select(g => new GroupWithMemberCount
            {
                Id = g.Id,
                Name = g.Name,
                CreatedAt = g.CreatedAt,
                MemberCount = g.UserGroups.Count,
                BoardType = (BoardType)g.BoardType,
                HomePage = (BoardPage)g.HomePage
            })
            .ToListAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId, CancellationToken token = default)
    {
        return await DbContext.Groups
            .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
            .Select(g => new GroupWithMemberCount
            {
                Id = g.Id,
                Name = g.Name,
                CreatedAt = g.CreatedAt,
                MemberCount = g.UserGroups.Count,
                BoardType = (BoardType)g.BoardType,
                HomePage = (BoardPage)g.HomePage
            })
            .ToListAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
        => await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token);

    /// <inheritdoc/>
    public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
    {
        // Check existence first — UserGroups has unique composite index on (UserId, GroupId)
        var exists = await DbContext.UserGroups
            .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
        if (exists)
            throw new InvalidOperationException("User is already a member of this group.");

        DbContext.UserGroups.Add(new UserGroupEntity
        {
            UserId = userId,
            GroupId = groupId,
            GroupRole = (int)groupRole
        });

        // A campaign board has no opt-in path for its events, so a member joining one must
        // already hold a row on every event dated today or later. The board type is read from
        // the group row named by the groupId argument rather than through the board-type
        // resolver service: that service answers for the caller's currently selected board,
        // which has no relationship to an arbitrary target board managed by route id (for
        // example the Platform group page). GroupEntity carries no query filter, so this read
        // is correct regardless of which board the caller has selected.
        var group = await DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
        if (group?.BoardType == (int)BoardType.Campaign)
        {
            var today = boardClock.Today;
            var futureEventIds = await GetFutureEventIdsForGroupIgnoringActiveBoardAsync(groupId, today, token);
            foreach (var eventId in futureEventIds)
            {
                // UpdatedAt is deliberately left at its default null: an automatic signup
                // carries no answered marker, which keeps it distinguishable from an answer
                // the member actually gave. Every member gets a row regardless of role —
                // a campaign board has no opt-in path, and filtering by role would lock
                // dungeon masters and admins out of the feature rather than merely omit them.
                DbContext.EventSignups.Add(new EventSignupEntity
                {
                    EventId = eventId,
                    UserId = userId,
                    Availability = (int)VoteType.Yes
                });
            }
        }

        try
        {
            // This save now also carries the automatic signup rows staged above, so a failure
            // here is no longer guaranteed to be the duplicate-membership race. The friendly
            // message is kept anyway because that race remains the only realistic trigger for
            // a member who was not already on the board.
            await DbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            // A concurrent request can win the race between the AnyAsync check above and this
            // insert; the table's unique index on (UserId, GroupId) then rejects the write.
            // Surface it as the same friendly exception the pre-check throws.
            throw new InvalidOperationException("User is already a member of this group.");
        }
    }

    /// <inheritdoc/>
    public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
    {
        var ug = await DbContext.UserGroups
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
        if (ug == null) return;
        DbContext.UserGroups.Remove(ug);

        // Everything this member holds on this board goes, past and future, whether the row
        // was automatic or deliberate. A campaign row is mostly an automatic default recording
        // nothing, so erasing it loses far less than erasing a record of something that
        // happened. Quest signups, date votes, characters, gold and transactions deliberately
        // survive a member leaving; extending this cleanup to them is out of scope.
        var signups = await GetEventSignupsForMemberIgnoringActiveBoardAsync(groupId, userId, token);
        DbContext.EventSignups.RemoveRange(signups);

        await DbContext.SaveChangesAsync(token);
    }

    // The ambient board filter answers for the caller's currently selected board, which is the
    // wrong question for an operation that targets a board named by an explicit groupId
    // argument. Scope is re-imposed immediately below by that same argument, so this query is
    // strictly narrower than an unscoped bypass rather than broader.
    private async Task<List<int>> GetFutureEventIdsForGroupIgnoringActiveBoardAsync(int groupId, DateOnly today, CancellationToken token)
    {
        return await DbContext.Events
            .IgnoreQueryFilters()
            .Where(e => e.GroupId == groupId && e.Date >= today)
            .Select(e => e.Id)
            .ToListAsync(token);
    }

    // Same reasoning as GetFutureEventIdsForGroupIgnoringActiveBoardAsync: the ambient filter
    // scopes to the caller's selected board, while this operation targets the board named by
    // the groupId argument, so scope is re-imposed explicitly from that argument.
    private async Task<List<EventSignupEntity>> GetEventSignupsForMemberIgnoringActiveBoardAsync(int groupId, int userId, CancellationToken token)
    {
        return await DbContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == userId && es.Event.GroupId == groupId)
            .ToListAsync(token);
    }

    // The ambient board filter answers for the caller's currently selected board, which is the
    // wrong question for an operation that targets a board named by an explicit groupId
    // argument. Each helper below re-imposes scope from that argument, so none is broader than
    // the board it is asked about. They are the single definition of what belongs to a board,
    // shared by the delete and its preview.
    private IQueryable<QuestEntity> BoardQuests(int groupId)
        => DbContext.Quests.IgnoreQueryFilters().Where(q => q.GroupId == groupId);

    private IQueryable<ProposedDateEntity> BoardProposedDates(int groupId)
        => DbContext.Set<ProposedDateEntity>().IgnoreQueryFilters().Where(pd => pd.Quest.GroupId == groupId);

    private IQueryable<PlayerDateVoteEntity> BoardDateVotes(int groupId)
        => DbContext.Set<PlayerDateVoteEntity>().IgnoreQueryFilters().Where(v => v.ProposedDate.Quest.GroupId == groupId);

    private IQueryable<PlayerSignupEntity> BoardPlayerSignups(int groupId)
        => DbContext.PlayerSignups.IgnoreQueryFilters().Where(ps => ps.Quest.GroupId == groupId);

    private IQueryable<ReminderLogEntity> BoardReminderLogs(int groupId)
        => DbContext.ReminderLogs.IgnoreQueryFilters().Where(r => r.Quest.GroupId == groupId);

    private IQueryable<EventEntity> BoardEvents(int groupId)
        => DbContext.Events.IgnoreQueryFilters().Where(e => e.GroupId == groupId);

    private IQueryable<EventSignupEntity> BoardEventSignups(int groupId)
        => DbContext.EventSignups.IgnoreQueryFilters().Where(es => es.Event.GroupId == groupId);

    private IQueryable<EventSeriesEntity> BoardEventSeries(int groupId)
        => DbContext.EventSeries.IgnoreQueryFilters().Where(s => s.GroupId == groupId);

    private IQueryable<CharacterEntity> BoardCharacters(int groupId)
        => DbContext.Characters.IgnoreQueryFilters().Where(c => c.GroupId == groupId);

    private IQueryable<CharacterClassEntity> BoardCharacterClasses(int groupId)
        => DbContext.CharacterClasses.IgnoreQueryFilters().Where(cc => cc.Character.GroupId == groupId);

    private IQueryable<ContactEntity> BoardContacts(int groupId)
        => DbContext.Contacts.IgnoreQueryFilters().Where(c => c.GroupId == groupId);

    private IQueryable<ContactNoteEntity> BoardContactNotes(int groupId)
        => DbContext.ContactNotes.IgnoreQueryFilters().Where(n => n.Contact.GroupId == groupId);

    private IQueryable<ContactCategoryEntity> BoardContactCategories(int groupId)
        => DbContext.ContactCategories.IgnoreQueryFilters().Where(c => c.GroupId == groupId);

    private IQueryable<ContactTagEntity> BoardContactTags(int groupId)
        => DbContext.ContactTags.IgnoreQueryFilters().Where(t => t.GroupId == groupId);

    private IQueryable<ShopItemEntity> BoardShopItems(int groupId)
        => DbContext.ShopItems.IgnoreQueryFilters().Where(s => s.GroupId == groupId);

    // A purchase, a sale or a return on one of this board's shop items goes with the board, and so
    // does any row chained to such a transaction through OriginalTransactionId, whoever made it.
    private IQueryable<UserTransactionEntity> BoardTransactions(int groupId)
        => DbContext.UserTransactions.IgnoreQueryFilters().Where(t =>
            t.ShopItem.GroupId == groupId
            || (t.OriginalTransaction != null && t.OriginalTransaction.ShopItem.GroupId == groupId));

    // The three helpers below find rows on OTHER boards that point into this one. They are only
    // ever used to clear that one pointer, never to remove the row, because the row belongs to
    // the other board.
    private IQueryable<QuestEntity> FollowUpsOnOtherBoards(int groupId)
        => DbContext.Quests.IgnoreQueryFilters().Where(q =>
            q.GroupId != groupId && q.OriginalQuest != null && q.OriginalQuest.GroupId == groupId);

    private IQueryable<PlayerSignupEntity> OtherBoardSignupsUsingBoardCharacters(int groupId)
        => DbContext.PlayerSignups.IgnoreQueryFilters().Where(ps =>
            ps.Quest.GroupId != groupId && ps.Character != null && ps.Character.GroupId == groupId);

    private IQueryable<EventEntity> OtherBoardEventsInBoardSeries(int groupId)
        => DbContext.Events.IgnoreQueryFilters().Where(e =>
            e.GroupId != groupId && e.Series != null && e.Series.GroupId == groupId);

    /// <inheritdoc/>
    public async Task<BoardDeletionResult> DeleteBoardAsync(int groupId, CancellationToken token = default)
    {
        var group = await DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
        if (group == null)
            return BoardDeletionResult.Refused(BoardDeleteStatus.NotFound);

        // Members are rechecked here, before anything is staged, because the check made when the
        // confirmation page was shown may be stale by the time it is confirmed.
        if (await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token))
            return BoardDeletionResult.Refused(BoardDeleteStatus.HasMembers, group.Name);

        // Rows on other boards that point into this board keep their place; only the pointer is
        // cleared, as a tracked write, so the delete never trips over them. A follow-up quest loses
        // its link to the original, a signup loses its character, and an event leaves the series
        // (the slot index goes with it because a series and its slot are only meaningful together).
        foreach (var followUp in await FollowUpsOnOtherBoards(groupId).ToListAsync(token))
            followUp.OriginalQuestId = null;

        foreach (var signup in await OtherBoardSignupsUsingBoardCharacters(groupId).ToListAsync(token))
            signup.CharacterId = null;

        foreach (var otherEvent in await OtherBoardEventsInBoardSeries(groupId).ToListAsync(token))
        {
            otherEvent.SeriesId = null;
            otherEvent.SeriesSlotIndex = null;
        }

        // EF orders the DELETE commands by the model's foreign keys inside the single save below,
        // which is one database transaction, so this staging order is for readability only.
        // Image rows (character, contact) and the contact-to-tag join rows are deliberately never
        // loaded: an image can be several megabytes, and the database removes those rows itself
        // when their character or contact goes. Membership rows cascade from the board too, and
        // the member check above guarantees there are none. A linked board set stays: only this
        // board's pointer to it goes, with the board row.
        var removed = new Dictionary<BoardContentKind, int>();

        Stage(BoardContentKind.DateVotes, await BoardDateVotes(groupId).ToListAsync(token));
        Stage(BoardContentKind.PlayerSignups, await BoardPlayerSignups(groupId).ToListAsync(token));
        Stage(BoardContentKind.ProposedDates, await BoardProposedDates(groupId).ToListAsync(token));
        Stage(BoardContentKind.ReminderLogs, await BoardReminderLogs(groupId).ToListAsync(token));
        Stage(BoardContentKind.Quests, await BoardQuests(groupId).ToListAsync(token));
        Stage(BoardContentKind.EventSignups, await BoardEventSignups(groupId).ToListAsync(token));
        Stage(BoardContentKind.Events, await BoardEvents(groupId).ToListAsync(token));
        Stage(BoardContentKind.EventSeries, await BoardEventSeries(groupId).ToListAsync(token));
        Stage(BoardContentKind.ContactNotes, await BoardContactNotes(groupId).ToListAsync(token));
        Stage(BoardContentKind.Contacts, await BoardContacts(groupId).ToListAsync(token));
        Stage(BoardContentKind.ContactCategories, await BoardContactCategories(groupId).ToListAsync(token));
        Stage(BoardContentKind.ContactTags, await BoardContactTags(groupId).ToListAsync(token));
        DbContext.CharacterClasses.RemoveRange(await BoardCharacterClasses(groupId).ToListAsync(token));
        Stage(BoardContentKind.Characters, await BoardCharacters(groupId).ToListAsync(token));
        Stage(BoardContentKind.Transactions, await BoardTransactions(groupId).ToListAsync(token));
        Stage(BoardContentKind.ShopItems, await BoardShopItems(groupId).ToListAsync(token));

        void Stage<TEntity>(BoardContentKind kind, List<TEntity> rows) where TEntity : class
        {
            DbContext.RemoveRange(rows);
            removed[kind] = rows.Count;
        }

        DbContext.Groups.Remove(group);

        try
        {
            await DbContext.SaveChangesAsync(token);
        }
        catch
        {
            // No half-applied tracked graph may be saved by a subsequent save in the same request.
            DbContext.ChangeTracker.Clear();
            throw;
        }

        return new BoardDeletionResult { Status = BoardDeleteStatus.Deleted, BoardName = group.Name, RemovedCounts = removed };
    }

    /// <inheritdoc/>
    public async Task<BoardDeletionPreview> GetDeletionPreviewAsync(int groupId, CancellationToken token = default)
    {
        // Names are sorted here rather than in the query so the order does not depend on the
        // database collation. Only the name column is read, never a whole row, and nothing is
        // saved.
        static List<string> SortedNames(List<string> names)
        {
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        static BoardDeletionItem Counted(BoardContentKind kind, int count)
            => new() { Kind = kind, Count = count };

        static BoardDeletionItem Named(BoardContentKind kind, List<string> names)
        {
            var sorted = SortedNames(names);
            return new BoardDeletionItem { Kind = kind, Count = sorted.Count, Names = sorted };
        }

        var items = new List<BoardDeletionItem>
        {
            Named(BoardContentKind.Quests, await BoardQuests(groupId).Select(q => q.Title).ToListAsync(token)),
            Counted(BoardContentKind.ProposedDates, await BoardProposedDates(groupId).CountAsync(token)),
            Counted(BoardContentKind.DateVotes, await BoardDateVotes(groupId).CountAsync(token)),
            Counted(BoardContentKind.PlayerSignups, await BoardPlayerSignups(groupId).CountAsync(token)),
            Counted(BoardContentKind.ReminderLogs, await BoardReminderLogs(groupId).CountAsync(token)),
            Named(BoardContentKind.Events, await BoardEvents(groupId).Select(e => e.Title).ToListAsync(token)),
            Counted(BoardContentKind.EventSignups, await BoardEventSignups(groupId).CountAsync(token)),
            Named(BoardContentKind.EventSeries, await BoardEventSeries(groupId).Select(s => s.Title).ToListAsync(token)),
            Named(BoardContentKind.Characters, await BoardCharacters(groupId).Select(c => c.Name).ToListAsync(token)),
            Named(BoardContentKind.Contacts, await BoardContacts(groupId).Select(c => c.Name).ToListAsync(token)),
            Counted(BoardContentKind.ContactNotes, await BoardContactNotes(groupId).CountAsync(token)),
            Named(BoardContentKind.ContactCategories, await BoardContactCategories(groupId).Select(c => c.Name).ToListAsync(token)),
            Named(BoardContentKind.ContactTags, await BoardContactTags(groupId).Select(t => t.Name).ToListAsync(token)),
            Named(BoardContentKind.ShopItems, await BoardShopItems(groupId).Select(s => s.Name).ToListAsync(token)),
            Counted(BoardContentKind.Transactions, await BoardTransactions(groupId).CountAsync(token))
        };

        var followUps = await FollowUpsOnOtherBoards(groupId)
            .Select(q => new BoardUnlinkedFollowUp { Title = q.Title, BoardName = q.Group.Name })
            .ToListAsync(token);
        followUps.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title));

        return new BoardDeletionPreview
        {
            Items = items,
            FollowUpsOnOtherBoards = followUps,
            SignupsLosingCharacter = await OtherBoardSignupsUsingBoardCharacters(groupId).CountAsync(token),
            EventsLeavingSeries = await OtherBoardEventsInBoardSeries(groupId).CountAsync(token)
        };
    }

    /// <inheritdoc/>
    public async Task<IList<UserGroup>> GetMembersAsync(int groupId, string? search = null, CancellationToken token = default)
    {
        var query = DbContext.UserGroups
            .Include(ug => ug.User)
            .Where(ug => ug.GroupId == groupId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(ug => ug.User!.Name.Contains(search) || (ug.User!.Email != null && ug.User!.Email.Contains(search)));
        }

        var entities = await query.ToListAsync(token);
        return Mapper.Map<IList<UserGroup>>(entities);
    }
}
