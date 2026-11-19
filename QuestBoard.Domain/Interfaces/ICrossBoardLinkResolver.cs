using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Decides which board a viewer may be taken to for a link to an entity of a given kind and id,
/// drawing only on boards the viewer belongs to. A board qualifies when it offers the page the
/// link opens and the viewer's role there can open it; a SuperAdmin counts as an Admin on every
/// board they are a member of and is never offered a board they do not belong to. A link that
/// cannot be taken anywhere looks the same whether the record does not exist or exists but may
/// not be opened.
/// </summary>
public interface ICrossBoardLinkResolver
{
    /// <summary>
    /// Returns the board the link may take the viewer to, restricted to boards the given user
    /// belongs to, that offer the page and whose role requirement the viewer meets, or null when
    /// no such board holds the entity -- a missing id, a board the user is not on, a board that
    /// does not offer the page and a role that may not open it all give the same answer on purpose.
    /// </summary>
    Task<CrossBoardTarget?> ResolveAsync(
        CrossBoardLookupKind kind,
        CrossBoardAccess access,
        BoardPage page,
        int id,
        int userId,
        bool viewerIsSuperAdmin,
        CancellationToken token = default);
}
