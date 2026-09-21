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
        return await dbContext.Quests
            .IgnoreQueryFilters()
            .Where(q => q.Id == questId && memberGroupIds.Contains(q.GroupId))
            .Select(q => (int?)q.GroupId)
            .FirstOrDefaultAsync(token);
    }
}
