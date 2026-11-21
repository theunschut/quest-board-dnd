using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

// This class answers one question -- which of the viewer's own boards may this link take them
// to -- and it answers it only from boards the viewer belongs to. A board qualifies when it
// offers the page the link opens and the viewer's role there can open that page. There is no
// separate answer for a record that exists but may not be opened: it ends in the same null as an
// id that does not exist, so the answer can never be used to learn that something is there.
// Rules that depend on the record itself (whose quest it is, whether a shop item is on sale) are
// applied by the repository inside its one lookup, from the scope built here: the viewer, the
// boards the viewer may review everything on, and the board's own time. Keeping the whole tenancy
// bypass in one place is what keeps it auditable.
internal sealed class CrossBoardLinkResolverService(IGroupService groupService, ICrossBoardLinkRepository repository, IBoardClock boardClock) : ICrossBoardLinkResolver
{
    /// <inheritdoc/>
    // The SuperAdmin flag only raises the viewer's role on boards they are already a member of.
    // Membership is read first and stays mandatory, so a SuperAdmin who is not a member of the
    // target board resolves to nothing, exactly like any other non-member.
    public async Task<CrossBoardTarget?> ResolveAsync(
        CrossBoardLookupKind kind,
        CrossBoardAccess access,
        BoardPage page,
        int id,
        int userId,
        bool viewerIsSuperAdmin,
        CancellationToken token = default)
    {
        // Read fresh on every call, never cached -- a membership that was revoked a second ago
        // must not still open a board.
        var memberships = await groupService.GetGroupsForUserAsync(userId, token);
        if (memberships.Count == 0)
        {
            return null;
        }

        var memberGroupIds = memberships.Select(g => g.Id).ToList();

        var viewerBoards = await repository.GetViewerBoardsAsync(userId, memberGroupIds, token);
        var eligibleBoards = viewerBoards
            .Where(board => BoardPagePolicy.IsAvailable(board.BoardType, board.DisabledPages, page)
                && RoleMeetsAccess(board.Role, access, viewerIsSuperAdmin))
            .ToList();
        var eligibleGroupIds = eligibleBoards.Select(board => board.GroupId).ToList();

        if (eligibleGroupIds.Count == 0)
        {
            return null;
        }

        if (kind == CrossBoardLookupKind.BoardMember)
        {
            return await ResolveBoardMemberAsync(id, memberships, eligibleGroupIds, token);
        }

        var scope = new CrossBoardVisibilityScope(
            userId,
            eligibleGroupIds,
            eligibleBoards.Where(board => EffectiveRole(board.Role, viewerIsSuperAdmin) >= GroupRole.DungeonMaster).Select(board => board.GroupId).ToList(),
            eligibleBoards.Where(board => EffectiveRole(board.Role, viewerIsSuperAdmin) == GroupRole.Admin).Select(board => board.GroupId).ToList(),
            boardClock.Now,
            boardClock.Today.ToDateTime(TimeOnly.MinValue));

        var resolvedGroupId = await repository.ResolveBoardIdAsync(kind, access, id, scope, token);
        if (resolvedGroupId is not { } targetGroupId)
        {
            return null;
        }

        var targetMembership = memberships.First(g => g.Id == targetGroupId);
        return new CrossBoardTarget(targetMembership.Id, targetMembership.Name);
    }

    private static GroupRole EffectiveRole(GroupRole role, bool viewerIsSuperAdmin) =>
        viewerIsSuperAdmin ? GroupRole.Admin : role;

    // What the viewer's role on a board has to be for the page behind the link to open at all. A
    // SuperAdmin counts as an Admin on boards they belong to. Access levels that turn on the
    // record rather than the role leave the role open here.
    private static bool RoleMeetsAccess(GroupRole role, CrossBoardAccess access, bool viewerIsSuperAdmin)
    {
        var effectiveRole = EffectiveRole(role, viewerIsSuperAdmin);

        return access switch
        {
            CrossBoardAccess.DungeonMasterTier or CrossBoardAccess.QuestOwnerOrAdmin => effectiveRole >= GroupRole.DungeonMaster,
            CrossBoardAccess.AdminForOtherUser => effectiveRole == GroupRole.Admin,
            _ => true
        };
    }

    // A profile link is only worth following automatically when there is exactly one board it
    // can mean. When the viewer and the target share several boards there is no right answer to
    // guess, and guessing would move the viewer's board for a page that would have rendered
    // anyway -- so today's behaviour stands instead. Boards that would refuse the viewer are
    // removed before that count, so a board that could not open the page never makes the answer
    // ambiguous. An absent id never reaches here at all, because the route reader requires an id
    // it can parse, which is what makes the self-edit form of the profile edit route a no-op by
    // construction.
    private async Task<CrossBoardTarget?> ResolveBoardMemberAsync(
        int targetUserId,
        IList<GroupWithMemberCount> memberships,
        IReadOnlyCollection<int> eligibleGroupIds,
        CancellationToken token)
    {
        var sharedGroupIds = await repository.ResolveSharedBoardIdsForUserAsync(targetUserId, eligibleGroupIds, token);
        if (sharedGroupIds.Count != 1)
        {
            return null;
        }

        var targetGroupId = sharedGroupIds[0];
        var targetMembership = memberships.First(g => g.Id == targetGroupId);
        return new CrossBoardTarget(targetMembership.Id, targetMembership.Name);
    }
}
