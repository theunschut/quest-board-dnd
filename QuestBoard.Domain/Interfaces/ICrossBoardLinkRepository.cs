using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// The one seam in the application permitted to read past the tenant query filters for the
/// purpose of answering "which board is this id on". Every implementation pins the caller's
/// membership set inside the query predicate, so the answer can only ever be a board the caller
/// already belongs to.
/// </summary>
public interface ICrossBoardLinkRepository
{
    /// <summary>
    /// Resolves the board an entity of the given kind and id lives on, restricted to the
    /// caller's own membership set. Returns null if the id does not exist, or exists on a board
    /// the caller is not a member of -- the two cases are indistinguishable on purpose.
    /// </summary>
    Task<int?> ResolveBoardIdAsync(CrossBoardLookupKind kind, int id, IReadOnlyCollection<int> memberGroupIds, CancellationToken token = default);

    /// <summary>
    /// Resolves every board a target user and the caller both belong to, restricted to the
    /// caller's own membership set.
    /// </summary>
    Task<IReadOnlyList<int>> ResolveSharedBoardIdsForUserAsync(int targetUserId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token = default);
}
