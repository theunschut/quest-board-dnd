using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;

namespace QuestBoard.Domain.Interfaces;

public interface IGroupService : IBaseService<Group>
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
    /// Throws InvalidOperationException if the user is already a member of the group.
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
    /// other boards that point into it. Refused with NameMismatch, and nothing changed, unless the
    /// typed name with surrounding whitespace trimmed equals the stored board name exactly (ordinal,
    /// case-sensitive). Returns HasMembers and changes nothing while the board has members, and
    /// NotFound when no such board exists.
    /// </summary>
    Task<BoardDeletionResult> DeleteBoardAsync(int groupId, string? confirmName, CancellationToken token = default);

    /// <summary>
    /// What deleting the board would remove, per kind of content, and which rows on other boards
    /// would only be unlinked. Changes nothing.
    /// </summary>
    Task<BoardDeletionPreview> GetDeletionPreviewAsync(int groupId, CancellationToken token = default);
}
