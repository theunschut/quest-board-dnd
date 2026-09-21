using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Resolves whether an entity of a given kind and id lives on a board the caller is a member
/// of, and if so which one.
/// </summary>
public interface ICrossBoardLinkResolver
{
    /// <summary>
    /// Returns the board an entity lives on, restricted to boards the given user belongs to, or
    /// null if the entity does not exist or is on a board the user does not belong to -- the two
    /// cases are indistinguishable on purpose.
    /// </summary>
    Task<CrossBoardTarget?> ResolveAsync(CrossBoardLookupKind kind, int id, int userId, CancellationToken token = default);
}
