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
}
