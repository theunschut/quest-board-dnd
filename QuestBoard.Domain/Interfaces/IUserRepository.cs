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
    /// Returns, per user id, the names of the boards the user is a member of, sorted A to Z ignoring case.
    /// Users who are members of no board are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetBoardNamesByUserAsync(CancellationToken token = default);

    /// <summary>
    /// Returns the given user's group role in the specified group, or null if they are not a member.
    /// </summary>
    Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId);

    /// <summary>
    /// Creates or updates the user's group-membership row with the specified role. Returns the UserGroup row Id.
    /// </summary>
    Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role);

    /// <summary>
    /// Changes the role on the user's existing group-membership row and never creates one. Returns false when the user
    /// has no membership of the group, including when the row is removed while the change is being saved, so a role
    /// change that races a removal cannot put the removed person back on the board.
    /// </summary>
    Task<bool> UpdateGroupRoleAsync(int userId, int groupId, GroupRole role, CancellationToken token = default);

    /// <summary>
    /// Returns, among the given user ids, those that hold the Admin group role on any group other than the given one.
    /// One query for any number of ids; an empty input returns an empty set without querying.
    /// </summary>
    Task<IReadOnlySet<int>> GetUserIdsAdministeringOtherGroupsAsync(IReadOnlyCollection<int> userIds, int groupId, CancellationToken token = default);
}
