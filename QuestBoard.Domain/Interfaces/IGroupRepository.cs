using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;

namespace QuestBoard.Domain.Interfaces;

public interface IGroupRepository : IBaseRepository<Group>
{
    /// <summary>
    /// Returns all groups with their member counts.
    /// </summary>
    Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default);

    /// <summary>
    /// Returns the groups the given user is a member of, with member counts.
    /// </summary>
    Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns whether the group has at least one member.
    /// </summary>
    Task<bool> HasMembersAsync(int groupId, CancellationToken token = default);

    /// <summary>
    /// Adds a user to a group with the given role.
    /// Throws InvalidOperationException if the user is already a member of the group (enforced ahead of the DB's unique composite index).
    /// </summary>
    Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default);

    /// <summary>
    /// Removes a user's membership from a group, if it exists.
    /// </summary>
    Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default);

    /// <summary>
    /// Returns all membership rows for a group, with user details loaded, optionally
    /// filtered by a search term matching the member's Name or Email (case-insensitive).
    /// </summary>
    Task<IList<UserGroup>> GetMembersAsync(int groupId, string? search = null, CancellationToken token = default);

    /// <summary>
    /// Deletes the board named by the id and everything scoped to it in one save, unlinking rows on
    /// other boards that point into it. Returns HasMembers and changes nothing while the board has
    /// members, and NotFound when no such board exists. A failed save clears the change tracker and
    /// rethrows.
    /// </summary>
    Task<BoardDeletionResult> DeleteBoardAsync(int groupId, CancellationToken token = default);

    /// <summary>
    /// What deleting the board would remove, per kind of content, and which rows on other boards
    /// would only be unlinked. Changes nothing and does not depend on the caller's active board.
    /// </summary>
    Task<BoardDeletionPreview> GetDeletionPreviewAsync(int groupId, CancellationToken token = default);
}
