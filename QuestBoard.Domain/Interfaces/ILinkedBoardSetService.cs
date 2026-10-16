using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ILinkedBoardSetService
{
    /// <summary>
    /// Returns every set ordered by name, each with its boards.
    /// </summary>
    Task<IList<LinkedBoardSet>> GetAllAsync(CancellationToken token = default);

    /// <summary>
    /// Returns the boards that are not in any set.
    /// </summary>
    Task<IList<LinkedBoardCandidate>> GetUnlinkedBoardsAsync(CancellationToken token = default);

    /// <summary>
    /// Creates an empty set of the given board type. The name is trimmed and must be unique
    /// ignoring case.
    /// </summary>
    Task<LinkedBoardSetOutcome> CreateAsync(string name, BoardType boardType, CancellationToken token = default);

    /// <summary>
    /// Puts a board into a set. Refused when the board's type differs from the set's type or the
    /// board is already in a set, whichever form the request came from.
    /// </summary>
    Task<LinkedBoardSetOutcome> AddBoardAsync(int setId, int groupId, CancellationToken token = default);
}
