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
                MemberCount = g.UserGroups.Count
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
                MemberCount = g.UserGroups.Count
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
        await DbContext.SaveChangesAsync(token);
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

    /// <inheritdoc/>
    public async Task<IList<UserGroup>> GetMembersAsync(int groupId, CancellationToken token = default)
    {
        var entities = await DbContext.UserGroups
            .Include(ug => ug.User)
            .Where(ug => ug.GroupId == groupId)
            .ToListAsync(token);
        return Mapper.Map<IList<UserGroup>>(entities);
    }
}
