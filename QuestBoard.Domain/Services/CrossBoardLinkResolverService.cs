using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

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
        var resolvedGroupId = await repository.ResolveBoardIdAsync(kind, id, memberGroupIds, token);
        if (resolvedGroupId is not { } targetGroupId)
        {
            return null;
        }

        var targetMembership = memberships.First(g => g.Id == targetGroupId);
        return new CrossBoardTarget(targetMembership.Id, targetMembership.Name);
    }
}
