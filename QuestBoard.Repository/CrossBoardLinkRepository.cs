using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

// Every cross-board id-to-board lookup in the application belongs in this one file, so the
// whole escape hatch past the tenant query filters is auditable in one place. Each method pins
// the caller's own membership set inside the query predicate before it ever touches the
// filtered tables, so the query can only ever answer with a board the caller already belongs
// to -- it cannot be used to discover a board the caller is not a member of. The one thing this
// class is permitted to leak is an integer board id the caller already knows they belong to;
// nothing here ever selects entity data. The viewer's own boards are read here too: their ids,
// the viewer's role, the board type and the switched-off pages, which is what a link needs to
// know before it may take the viewer anywhere.
//
// A lookup also applies the rule the target page applies to the record itself (whose it is,
// whether it is on sale, whether the quest has been played), from the scope the caller built.
// The rule sits inside the one query for the kind, so a record the viewer may not open ends in
// the same null as an id that does not exist.
internal sealed class CrossBoardLinkRepository(QuestBoardContext dbContext) : ICrossBoardLinkRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<CrossBoardViewerBoard>> GetViewerBoardsAsync(int viewerId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token = default)
    {
        // Pinned to the viewer's own membership rows and to the membership set the caller passed,
        // so a board the viewer does not belong to can never appear. The projection selects
        // ids, the role, the board type and the disabled-page mask only -- no board content.
        // Like the shared-board lookup below, this reads across boards on purpose, so it keeps
        // working if membership ever gains a board-scoped filter.
        var rows = await dbContext.UserGroups
            .IgnoreQueryFilters()
            .Where(ug => ug.UserId == viewerId && memberGroupIds.Contains(ug.GroupId))
            .Select(ug => new { ug.GroupId, ug.GroupRole, ug.Group.BoardType, ug.Group.DisabledPages })
            .ToListAsync(token);

        return rows
            .Select(r => new CrossBoardViewerBoard(r.GroupId, (GroupRole)r.GroupRole, (BoardType)r.BoardType, new BoardPageSet(r.DisabledPages)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<int?> ResolveBoardIdAsync(CrossBoardLookupKind kind, CrossBoardAccess access, int id, CrossBoardVisibilityScope scope, CancellationToken token = default)
    {
        var memberGroupIds = scope.CandidateGroupIds;

        return kind switch
        {
            CrossBoardLookupKind.Quest => await ResolveQuestBoardIdAsync(access, id, scope, token),
            // Event, event series, contact category, and the Member, DungeonMasterTier and
            // AdminForOtherUser rows of the kinds above were checked against their pages and have
            // no rule that turns on the record itself, so only the membership pin applies.
            CrossBoardLookupKind.Event => await ResolveEventBoardIdAsync(id, memberGroupIds, token),
            CrossBoardLookupKind.Character => await ResolveCharacterBoardIdAsync(access, id, scope, token),
            CrossBoardLookupKind.Contact => await ResolveContactBoardIdAsync(access, id, scope, token),
            CrossBoardLookupKind.ShopItem => await ResolveShopItemBoardIdAsync(access, id, scope, token),
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
    //
    // A record the viewer could not open on its own page ends in the same null as an id that does
    // not exist, because the page's rule is part of the one query and there is no second lookup
    // for a record that exists but is refused.
    private async Task<int?> ResolveQuestBoardIdAsync(CrossBoardAccess access, int questId, CrossBoardVisibilityScope scope, CancellationToken token)
    {
        // Also serves the quest log's routes -- the quest log has no table of its own, and
        // QuestLogController resolves everything through the quest service.
        var memberGroupIds = scope.CandidateGroupIds;

        var query = dbContext.Quests
            .IgnoreQueryFilters()
            .Where(q => q.Id == questId && memberGroupIds.Contains(q.GroupId));

        if (access == CrossBoardAccess.QuestOwnerOrAdmin)
        {
            // The edit and follow-up pages are for the quest's own Dungeon Master, or an Admin of
            // the board it lives on (a SuperAdmin member counts as one).
            var viewerId = scope.ViewerId;
            var adminGroupIds = scope.AdminGroupIds;
            query = query.Where(q => q.DungeonMasterId == viewerId || adminGroupIds.Contains(q.GroupId));
        }
        else if (access == CrossBoardAccess.CompletedQuest)
        {
            // The quest log shows a closed quest, or a finalized one-shot whose game night is
            // before the start of the board's own today and that is not a Dungeon Master session.
            // This is the query form of QuestExtensions.HasBeenPlayed, and a unit fact keeps the
            // two in agreement.
            var todayStart = scope.BoardTodayStart;
            query = query.Where(q =>
                q.IsClosed
                || (q.IsFinalized && q.FinalizedDate != null && q.FinalizedDate < todayStart && !q.DungeonMasterSession));
        }

        return await query
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

    private async Task<int?> ResolveCharacterBoardIdAsync(CrossBoardAccess access, int characterId, CrossBoardVisibilityScope scope, CancellationToken token)
    {
        var memberGroupIds = scope.CandidateGroupIds;

        var query = dbContext.Characters
            .IgnoreQueryFilters()
            .Where(c => c.Id == characterId && memberGroupIds.Contains(c.GroupId));

        if (access == CrossBoardAccess.CharacterOwnerOrAdmin)
        {
            // The edit page is for the character's owner, or an Admin of the board it lives on
            // (a SuperAdmin member counts as one).
            var viewerId = scope.ViewerId;
            var adminGroupIds = scope.AdminGroupIds;
            query = query.Where(c => c.OwnerId == viewerId || adminGroupIds.Contains(c.GroupId));
        }

        return await query
            .Select(c => (int?)c.GroupId)
            .FirstOrDefaultAsync(token);
    }

    private async Task<int?> ResolveContactBoardIdAsync(CrossBoardAccess access, int contactId, CrossBoardVisibilityScope scope, CancellationToken token)
    {
        var memberGroupIds = scope.CandidateGroupIds;

        var query = dbContext.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.Id == contactId && memberGroupIds.Contains(c.GroupId));

        if (access == CrossBoardAccess.ContactVisible)
        {
            // A contact is shown when it is revealed, or the viewer created it, or the viewer is a
            // Dungeon Master, Admin or SuperAdmin member of its board. The page also lets such a
            // viewer hide unrevealed contacts behind a per-session toggle; the switch never reads
            // session state, so it treats the toggle as on.
            var viewerId = scope.ViewerId;
            var tierGroupIds = scope.DungeonMasterTierGroupIds;
            query = query.Where(c => c.IsRevealed || c.CreatedByUserId == viewerId || tierGroupIds.Contains(c.GroupId));
        }

        return await query
            .Select(c => (int?)c.GroupId)
            .FirstOrDefaultAsync(token);
    }

    // A shop item's page shows it to members only once it is Published or Archived and its sale
    // has started; a Dungeon Master, an Admin or a SuperAdmin member of the board sees every
    // status. The second half is the query form of ShopItemAvailability.IsVisibleToMembers, and a
    // unit fact keeps the two in agreement. The rule sits inside the one query, so a hidden item
    // ends in the same null as an id that does not exist.
    private async Task<int?> ResolveShopItemBoardIdAsync(CrossBoardAccess access, int shopItemId, CrossBoardVisibilityScope scope, CancellationToken token)
    {
        var memberGroupIds = scope.CandidateGroupIds;

        var query = dbContext.ShopItems
            .IgnoreQueryFilters()
            .Where(s => s.Id == shopItemId && memberGroupIds.Contains(s.GroupId));

        if (access == CrossBoardAccess.ShopItemVisible)
        {
            var tierGroupIds = scope.DungeonMasterTierGroupIds;
            var boardNow = scope.BoardNow;
            query = query.Where(s =>
                tierGroupIds.Contains(s.GroupId)
                || ((s.Status == (int)ItemStatus.Published || s.Status == (int)ItemStatus.Archived)
                    && (s.AvailableFrom == null || s.AvailableFrom <= boardNow)));
        }

        return await query
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
