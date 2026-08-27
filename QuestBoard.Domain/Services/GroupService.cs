using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
{
    /// <inheritdoc/>
    public async Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default)
        => await repository.GetAllWithMemberCountAsync(token);

    /// <inheritdoc/>
    public async Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId, CancellationToken token = default)
        => await repository.GetGroupsForUserAsync(userId, token);

    /// <inheritdoc/>
    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
        => await repository.HasMembersAsync(groupId, token);

    /// <summary>
    /// The single chokepoint every membership addition funnels through — the Platform group
    /// page and the invite flow via the user service both call this and nowhere else. Anything
    /// that must happen when a member joins a board belongs behind it.
    /// </summary>
    /// <inheritdoc/>
    public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
        => await repository.AddMemberAsync(groupId, userId, groupRole, token);

    /// <summary>
    /// The single chokepoint every membership removal funnels through — the Platform group
    /// page and admin user removal both call this and nowhere else. Anything that must happen
    /// when a member leaves a board belongs behind it.
    /// </summary>
    /// <inheritdoc/>
    public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
        => await repository.RemoveMemberAsync(groupId, userId, token);

    /// <inheritdoc/>
    public async Task<IList<UserGroup>> GetMembersAsync(int groupId, string? search = null, CancellationToken token = default)
        => await repository.GetMembersAsync(groupId, search, token);

    /// <inheritdoc/>
    public override async Task AddAsync(Group model, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("Group name is required.", nameof(model));
        model.CreatedAt = DateTime.UtcNow;
        await base.AddAsync(model, token);
        // DbUpdateException for unique name violation bubbles up to the caller (GroupController)
    }
}
