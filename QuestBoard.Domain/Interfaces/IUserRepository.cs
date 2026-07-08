using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IUserRepository : IBaseRepository<User>
{
    /// <summary>
    /// Returns whether a user with the given name already exists (case-insensitive).
    /// </summary>
    Task<bool> ExistsAsync(string name);

    /// <summary>
    /// Returns all users holding the DungeonMaster or Admin group role in the active group.
    /// Returns an empty list when there is no active group.
    /// </summary>
    Task<IList<User>> GetAllDungeonMasters(CancellationToken token = default);

    /// <summary>
    /// Returns all users holding the Player group role in the active group.
    /// Returns an empty list when there is no active group.
    /// </summary>
    Task<IList<User>> GetAllPlayers(CancellationToken token = default);

    /// <summary>
    /// Returns all members of the specified group, regardless of their group role.
    /// </summary>
    Task<IList<User>> GetAllGroupMembers(int groupId, CancellationToken token = default);

    /// <summary>
    /// Returns users who are NOT members of the given group, optionally filtered by a search term
    /// matching Name or Email (case-insensitive).
    /// </summary>
    Task<IList<User>> GetAvailableUsers(int groupId, string? search, CancellationToken token = default);

    /// <summary>
    /// Returns the given user's group role in the specified group, or null if they are not a member.
    /// </summary>
    Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId);

    /// <summary>
    /// Creates or updates the user's group-membership row with the specified role. Returns the UserGroup row Id.
    /// </summary>
    Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role);
}
