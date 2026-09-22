using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

// This class answers exactly one question -- which board owns this id -- and nothing about
// whether the viewer should be allowed to see or act on it once there. A cancelled event, an
// archived shop item, or a page the viewer is only a Player on all still resolve here; the page
// itself is what applies visibility rules and authorization policy. Keeping those concerns out
// of this class is what keeps the whole tenancy bypass auditable in one place.
internal sealed class CrossBoardLinkResolverService(IGroupService groupService, ICrossBoardLinkRepository repository) : ICrossBoardLinkResolver
{
    /// <inheritdoc/>
    // Takes a plain int userId and nothing else -- no ClaimsPrincipal, no role flag -- so there is
    // nowhere a "but this viewer is a SuperAdmin" branch could be added without changing the
    // signature. A SuperAdmin who is not a member of the target board resolves to nothing here,
    // exactly like any other non-member.
    public async Task<CrossBoardTarget?> ResolveAsync(CrossBoardLookupKind kind, int id, int userId, CancellationToken token = default)
    {
        // Read fresh on every call, never cached -- a membership that was revoked a second ago
        // must not still open a board.
        var memberships = await groupService.GetGroupsForUserAsync(userId, token);
        if (memberships.Count == 0)
        {
            return null;
        }

        var memberGroupIds = memberships.Select(g => g.Id).ToList();

        if (kind == CrossBoardLookupKind.BoardMember)
        {
            return await ResolveBoardMemberAsync(id, memberships, memberGroupIds, token);
        }

        var resolvedGroupId = await repository.ResolveBoardIdAsync(kind, id, memberGroupIds, token);
        if (resolvedGroupId is not { } targetGroupId)
        {
            return null;
        }

        var targetMembership = memberships.First(g => g.Id == targetGroupId);
        return new CrossBoardTarget(targetMembership.Id, targetMembership.Name);
    }

    // A profile link is only worth following automatically when there is exactly one board it
    // can mean. When the viewer and the target share several boards there is no right answer to
    // guess, and guessing would move the viewer's board for a page that would have rendered
    // anyway -- so today's behaviour stands instead. An absent id never reaches here at all,
    // because the route reader requires an id it can parse, which is what makes the self-edit
    // form of the profile edit route a no-op by construction.
    private async Task<CrossBoardTarget?> ResolveBoardMemberAsync(
        int targetUserId,
        IList<GroupWithMemberCount> memberships,
        IReadOnlyCollection<int> memberGroupIds,
        CancellationToken token)
    {
        var sharedGroupIds = await repository.ResolveSharedBoardIdsForUserAsync(targetUserId, memberGroupIds, token);
        if (sharedGroupIds.Count != 1)
        {
            return null;
        }

        var targetGroupId = sharedGroupIds[0];
        var targetMembership = memberships.First(g => g.Id == targetGroupId);
        return new CrossBoardTarget(targetMembership.Id, targetMembership.Name);
    }
}
