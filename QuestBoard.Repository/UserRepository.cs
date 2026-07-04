using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class UserRepository(QuestBoardContext dbContext, IMapper mapper, IActiveGroupContext activeGroupContext)
    : BaseRepository<User, UserEntity>(dbContext, mapper), IUserRepository
{
    /// <inheritdoc/>
    public virtual async Task<bool> ExistsAsync(string name)
    {
        return await DbSet.AnyAsync(u => u.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAllDungeonMasters(CancellationToken token = default)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return [];

        var entities = await DbSet
            .Where(u => DbContext.UserGroups
                .Any(ug => ug.UserId == u.Id
                        && ug.GroupId == groupId.Value
                        && (ug.GroupRole == (int)GroupRole.DungeonMaster
                            || ug.GroupRole == (int)GroupRole.Admin)))
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<User>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAllPlayers(CancellationToken token = default)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return [];

        var entities = await DbSet
            .Where(u => DbContext.UserGroups
                .Any(ug => ug.UserId == u.Id
                        && ug.GroupId == groupId.Value
                        && ug.GroupRole == (int)GroupRole.Player))
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<User>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAllGroupMembers(int groupId, CancellationToken token = default)
    {
        // Membership is any UserGroups row for the group, regardless of role.
        var entities = await DbSet
            .Where(u => DbContext.UserGroups
                .Any(ug => ug.UserId == u.Id && ug.GroupId == groupId))
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<User>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAvailableUsers(int groupId, string? search, CancellationToken token = default)
    {
        var query = DbSet
            .Where(u => !DbContext.UserGroups
                .Any(ug => ug.UserId == u.Id && ug.GroupId == groupId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Name.Contains(search) || (u.Email != null && u.Email.Contains(search)));
        }

        var entities = await query.ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<User>>(entities);
    }

    /// <inheritdoc/>
    public async Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId)
    {
        var ug = await DbContext.UserGroups
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
        if (ug == null) return null;
        return (GroupRole)ug.GroupRole;
    }

    /// <inheritdoc/>
    public async Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role)
    {
        var ug = await DbContext.UserGroups
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
        if (ug == null)
        {
            ug = new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role };
            DbContext.UserGroups.Add(ug);
        }
        else
        {
            ug.GroupRole = (int)role;
        }
        await DbContext.SaveChangesAsync();
        return ug.Id;
    }
}
