using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper)
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
                BoardType = (BoardType)g.BoardType
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
                BoardType = (BoardType)g.BoardType
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
            var today = DateOnly.FromDateTime(DateTime.Today);
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
