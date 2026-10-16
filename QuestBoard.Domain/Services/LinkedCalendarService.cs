using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class LinkedCalendarService(
    ILinkedBoardSetRepository setRepository,
    IQuestRepository questRepository,
    IEventRepository eventRepository,
    ILogger<LinkedCalendarService> logger) : ILinkedCalendarService
{
    /// <inheritdoc/>
    public async Task<LinkedCalendarData> GetForViewerAsync(
        int? activeGroupId,
        int? viewerUserId,
        bool viewerIsSuperAdmin,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken token = default)
    {
        // No active board or no resolvable viewer means there is no set to look up, so nothing
        // is read across boards. RequireActiveGroupId is deliberately not used: a site admin
        // with no active board must get an empty overlay, not an exception.
        if (activeGroupId is not { } groupId || viewerUserId is not { } userId)
        {
            return LinkedCalendarData.Empty;
        }

        // The set and the viewer's role on each board are read fresh here on every request,
        // never cached, so removing a board from a set or a member from a board takes effect on
        // the very next page load.
        var linked = await setRepository.GetLinkedBoardsForViewerAsync(groupId, userId, token);
        if (linked.Boards.Count == 0)
        {
            return LinkedCalendarData.Empty;
        }

        var boards = linked.Boards;
        var linkedIds = boards.Select(b => b.Id).ToList();
        var boardsById = boards.ToDictionary(b => b.Id);

        var fetchedEvents = await eventRepository.GetLinkedEventsForCalendarAsync(
            linkedIds, DateOnly.FromDateTime(fromInclusive), DateOnly.FromDateTime(toExclusive), token);

        // The query and this check both read the same linkedIds, so this does not catch a wrong
        // set. It catches a dropped predicate or a bad containment translation in the query,
        // which would otherwise hand another board's rows to this page.
        var events = fetchedEvents.Where(e => boardsById.ContainsKey(e.GroupId)).ToList();
        if (events.Count != fetchedEvents.Count)
        {
            logger.LogError(
                "Linked calendar dropped {DroppedCount} of {FetchedCount} event row(s) outside the linked set of board {ActiveGroupId}",
                fetchedEvents.Count - events.Count, fetchedEvents.Count, groupId);
        }

        foreach (var entry in events)
        {
            var board = boardsById[entry.GroupId];
            entry.BoardName = board.Name;
            entry.ViewerIsMember = board.ViewerIsMember;
        }

        return new LinkedCalendarData
        {
            Boards = boards,
            Events = events,
            Quests = []
        };
    }
}
