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
    /// already taken.
    /// </summary>
    Task<int?> CreateAsync(string name, BoardType boardType, CancellationToken token = default);

    /// <summary>
    /// Puts a board into a set, or takes it out of its set when <paramref name="setId"/> is null.
    /// Returns false when the board does not exist.
    /// </summary>
    Task<bool> AssignBoardAsync(int groupId, int? setId, CancellationToken token = default);

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
    /// Renames a set. Returns false when the set does not exist or the name is already taken.
    /// </summary>
    Task<bool> RenameAsync(int setId, string name, CancellationToken token = default);

    /// <summary>
    /// Deletes a set and un-links every board in it. No board, quest or event is deleted.
    /// Returns false when the set does not exist.
    /// </summary>
    Task<bool> DeleteAsync(int setId, CancellationToken token = default);
}
