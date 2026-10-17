using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ILinkedBoardSetRepository
{
    /// <summary>
    /// Returns every set ordered by name, each with its boards ordered by name. A set holding
    /// fewer than two boards is still returned.
    /// </summary>
    Task<IList<LinkedBoardSet>> GetAllAsync(CancellationToken token = default);

    /// <summary>
    /// Returns one set with its boards ordered by name, or null when the set does not exist.
    /// </summary>
    Task<LinkedBoardSet?> GetByIdAsync(int setId, CancellationToken token = default);

    /// <summary>
    /// Creates an empty set of the given board type and returns its id, or null when the name is
    /// already taken. Any other failure to save is thrown, not reported as a taken name.
    /// </summary>
    Task<int?> CreateAsync(string name, BoardType boardType, CancellationToken token = default);

    /// <summary>
    /// Puts a board that is in no set into a set. The write only lands if the board is still in no
    /// set, so a board that another change put into a set meanwhile is never moved out of it.
    /// Returns <see cref="LinkedBoardSetResult.AlreadyLinked"/> when the board is in a set,
    /// <see cref="LinkedBoardSetResult.NotFound"/> when the board or the set does not exist, and
    /// <see cref="LinkedBoardSetResult.Success"/> otherwise. It does not check board types.
    /// </summary>
    Task<LinkedBoardSetResult> AddBoardAsync(int setId, int groupId, CancellationToken token = default);

    /// <summary>
    /// Takes a board out of a set. The write only lands if the board is still in that set.
    /// Returns <see cref="LinkedBoardSetResult.NotInSet"/> when the board is not in the set,
    /// <see cref="LinkedBoardSetResult.NotFound"/> when the board does not exist, and
    /// <see cref="LinkedBoardSetResult.Success"/> otherwise.
    /// </summary>
    Task<LinkedBoardSetResult> RemoveBoardAsync(int setId, int groupId, CancellationToken token = default);

    /// <summary>
    /// Returns the boards that are not in any set, ordered by name.
    /// </summary>
    Task<IList<LinkedBoardCandidate>> GetUnlinkedBoardsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns one board's name, type and set, or null when the board does not exist.
    /// </summary>
    Task<LinkedBoardCandidate?> GetBoardAsync(int groupId, CancellationToken token = default);

    /// <summary>
    /// Returns whether a set already uses the name, ignoring case. A set can be excluded so it
    /// does not collide with itself when it is renamed.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludingSetId, CancellationToken token = default);

    /// <summary>
    /// Renames a set. Returns <see cref="LinkedBoardSetResult.NotFound"/> when the set does not
    /// exist or was deleted meanwhile, <see cref="LinkedBoardSetResult.DuplicateName"/> when another
    /// set holds the name, and <see cref="LinkedBoardSetResult.Success"/> otherwise. Any other
    /// failure to save is thrown, not reported as a taken name.
    /// </summary>
    Task<LinkedBoardSetResult> RenameAsync(int setId, string name, CancellationToken token = default);

    /// <summary>
    /// Deletes a set and un-links every board in it. No board, quest or event is deleted.
    /// Returns false when the set does not exist or was deleted meanwhile.
    /// </summary>
    Task<bool> DeleteAsync(int setId, CancellationToken token = default);

    /// <summary>
    /// Returns the other boards in the active board's set, each with the viewer's role on it
    /// (null when the viewer is not a member). Read fresh on every call. Returns no boards when
    /// the active board is in no set, is alone in its set, or its type differs from the set's,
    /// and a board whose type differs from the set's is left out. Being a member here is the
    /// same membership the deep-link resolver checks; there is no site-admin shortcut.
    /// </summary>
    Task<ViewerLinkedBoards> GetLinkedBoardsForViewerAsync(int activeGroupId, int viewerUserId, CancellationToken token = default);
}
