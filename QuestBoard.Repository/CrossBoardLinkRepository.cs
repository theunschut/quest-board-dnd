using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

// Every cross-board id-to-board lookup in the application belongs in this one file, so the
// whole escape hatch past the tenant query filters is auditable in one place. Each method pins
// the caller's own membership set inside the query predicate before it ever touches the
// filtered tables, so the query can only ever answer with a board the caller already belongs
// to -- it cannot be used to discover a board the caller is not a member of. The one thing this
// class is permitted to leak is an integer board id the caller already knows they belong to;
// nothing here ever selects entity data.
internal sealed class CrossBoardLinkRepository(QuestBoardContext dbContext) : ICrossBoardLinkRepository
{
    /// <inheritdoc/>
    public async Task<int?> ResolveBoardIdAsync(CrossBoardLookupKind kind, int id, IReadOnlyCollection<int> memberGroupIds, CancellationToken token = default)
    {
        return kind switch
        {
            CrossBoardLookupKind.Quest => await ResolveQuestBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.Event => await ResolveEventBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.Character => await ResolveCharacterBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.Contact => await ResolveContactBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.ShopItem => await ResolveShopItemBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.EventSeries => await ResolveEventSeriesBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.ContactCategory => await ResolveContactCategoryBoardIdAsync(id, memberGroupIds, token),
            // BoardMember has a different answer shape -- the id is a target user, not an
            // entity, and the ambiguity rule needs the whole set of shared boards, not the
            // first match. A caller that reaches for this method with BoardMember anyway has
            // the wrong method, and gets a loud failure instead of a plausible-looking wrong
            // answer -- use ResolveSharedBoardIdsForUserAsync instead.
            CrossBoardLookupKind.BoardMember => throw new InvalidOperationException(
                $"{nameof(CrossBoardLookupKind.BoardMember)} is resolved through {nameof(ResolveSharedBoardIdsForUserAsync)}, not {nameof(ResolveBoardIdAsync)}."),
            // A lookup kind with no projection here is a build-time-visible mistake, not a
            // silent "no board found" -- the two would otherwise be indistinguishable from a
            // legitimate non-member miss, which is exactly the ambiguity this repository must
            // never introduce.
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No projection registered for this lookup kind.")
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ResolveSharedBoardIdsForUserAsync(int targetUserId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token = default)
    {
        // The caller's own membership set is pinned inside the predicate, so this can only ever
        // report boards the caller already belongs to -- never a board the target user is on
        // that the caller is not.
        //
        // Membership rows carry no query filter today, so IgnoreQueryFilters() bypasses nothing
        // here; it is deliberate and the one call in this class that is. If membership ever gains
        // a board-scoped filter, this lookup still has to read across boards or cross-board
        // resolution stops working -- and it would fail as a plausible-looking "not a member"
        // rather than as an error, which is the hardest kind of break to notice.
        return await dbContext.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == targetUserId && memberGroupIds.Contains(ug.GroupId))
            .Select(ug => ug.GroupId)
            .Distinct()
            .ToListAsync(token);
    }

    // The caller's membership set is pinned inside the predicate, so the query can only ever
    // answer with a board the caller already belongs to. The projection selects only GroupId --
    // no entity, no title, no navigation property, ever.
    private async Task<int?> ResolveQuestBoardIdAsync(int questId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        // Also serves the quest log's routes -- the quest log has no table of its own, and
        // QuestLogController resolves everything through the quest service.
        return await dbContext.Quests
            .IgnoreQueryFilters()
            .Where(q => q.Id == questId && memberGroupIds.Contains(q.GroupId))
            .Select(q => (int?)q.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveEventBoardIdAsync(int eventId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        return await dbContext.Events
            .IgnoreQueryFilters()
            .Where(e => e.Id == eventId && memberGroupIds.Contains(e.GroupId))
            .Select(e => (int?)e.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveCharacterBoardIdAsync(int characterId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        return await dbContext.Characters
            .IgnoreQueryFilters()
            .Where(c => c.Id == characterId && memberGroupIds.Contains(c.GroupId))
            .Select(c => (int?)c.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveContactBoardIdAsync(int contactId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        return await dbContext.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.Id == contactId && memberGroupIds.Contains(c.GroupId))
            .Select(c => (int?)c.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveShopItemBoardIdAsync(int shopItemId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        return await dbContext.ShopItems
            .IgnoreQueryFilters()
            .Where(s => s.Id == shopItemId && memberGroupIds.Contains(s.GroupId))
            .Select(s => (int?)s.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveEventSeriesBoardIdAsync(int eventSeriesId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        return await dbContext.EventSeries
            .IgnoreQueryFilters()
            .Where(es => es.Id == eventSeriesId && memberGroupIds.Contains(es.GroupId))
            .Select(es => (int?)es.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveContactCategoryBoardIdAsync(int contactCategoryId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
    {
        return await dbContext.ContactCategories
            .IgnoreQueryFilters()
            .Where(cc => cc.Id == contactCategoryId && memberGroupIds.Contains(cc.GroupId))
            .Select(cc => (int?)cc.GroupId)
            .FirstOrDefaultAsync(token);
    }
}
