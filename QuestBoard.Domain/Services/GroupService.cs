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

    /// <inheritdoc/>
    public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
        => await repository.AddMemberAsync(groupId, userId, groupRole, token);

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
