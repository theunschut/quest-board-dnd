using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
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

        List<LinkedQuestEntry> quests = linked.SetBoardType == BoardType.Campaign
            ? []
            : await GetVisibleQuestsAsync(boardsById, linkedIds, viewerIsSuperAdmin, fromInclusive, toExclusive, groupId, token);

        return new LinkedCalendarData
        {
            Boards = boards,
            Events = events,
            Quests = quests
        };
    }

    // A campaign set is an events-only surface, exactly like a campaign board's own calendar, so
    // its quests are never fetched rather than fetched and hidden; the caller skips this method.
    private async Task<List<LinkedQuestEntry>> GetVisibleQuestsAsync(
        Dictionary<int, LinkedBoard> boardsById,
        List<int> linkedIds,
        bool viewerIsSuperAdmin,
        DateTime fromInclusive,
        DateTime toExclusive,
        int activeGroupId,
        CancellationToken token)
    {
        var fetchedQuests = await questRepository.GetLinkedQuestsForCalendarAsync(
            linkedIds, fromInclusive, toExclusive, token);

        // Same reasoning as the event check above: both layers read the same linkedIds.
        var quests = fetchedQuests.Where(q => boardsById.ContainsKey(q.GroupId)).ToList();
        if (quests.Count != fetchedQuests.Count)
        {
            logger.LogError(
                "Linked calendar dropped {DroppedCount} of {FetchedCount} quest row(s) outside the linked set of board {ActiveGroupId}",
                fetchedQuests.Count - quests.Count, fetchedQuests.Count, activeGroupId);
        }

        // A DM-only session is visible only to a DungeonMaster or Admin of the board that owns
        // it, or to a site admin (who counts as Admin). The role is the viewer's role on the
        // owning board, not on the board being viewed, so a DungeonMaster of the linked board
        // who is only a player here still sees the linked board's sessions.
        var visible = quests
            .Where(q => !q.DungeonMasterSession
                || viewerIsSuperAdmin
                || boardsById[q.GroupId].ViewerRole is GroupRole.DungeonMaster or GroupRole.Admin)
            .ToList();

        foreach (var entry in visible)
        {
            var board = boardsById[entry.GroupId];
            entry.BoardName = board.Name;
            entry.ViewerIsMember = board.ViewerIsMember;
        }

        return visible;
    }
}
