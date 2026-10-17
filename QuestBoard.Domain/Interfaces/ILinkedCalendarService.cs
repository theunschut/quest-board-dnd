using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ILinkedCalendarService
{
    /// <summary>
    /// Returns what the other boards in the active board's linked set add to a calendar for the
    /// window [<paramref name="fromInclusive"/>, <paramref name="toExclusive"/>): the boards
    /// themselves plus their events and quests, read-only. The linked set and the viewer's
    /// membership are read fresh on every call. When there is no active board, no viewer, or no
    /// other board in the set, nothing is read across boards and an empty result comes back.
    /// </summary>
    Task<LinkedCalendarData> GetForViewerAsync(
        int? activeGroupId,
        int? viewerUserId,
        bool viewerIsSuperAdmin,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken token = default);
}
