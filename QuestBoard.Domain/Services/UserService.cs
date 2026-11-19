using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace QuestBoard.Domain.Services;

internal class UserService(IIdentityService identityService, IUserRepository repository, IMapper mapper, IGroupService groupService) : BaseService<User>(repository, mapper), IUserService
{
    /// <inheritdoc/>
    public async Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal user, string oldPassword, string newPassword)
    {
        return await identityService.ChangePasswordAsync(user, oldPassword, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword)
    {
        return await identityService.ChangePasswordAsync(user.Id, oldPassword, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> CreateAsync(string email, string name)
    {
        return await identityService.CreateUserAsync(email, name);
    }

    /// <inheritdoc/>
    public virtual async Task<bool> ExistsAsync(string name)
    {
        return await repository.ExistsAsync(name);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAllDungeonMastersAsync(CancellationToken token = default)
    {
        return await repository.GetAllDungeonMasters(token);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAllPlayersAsync(CancellationToken token = default)
    {
        return await repository.GetAllPlayers(token);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAllGroupMembersAsync(int groupId, CancellationToken token = default)
    {
        return await repository.GetAllGroupMembers(groupId, token);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetBoardNamesByUserAsync(CancellationToken token = default)
    {
        return await repository.GetBoardNamesByUserAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<User>> GetAvailableUsersAsync(int groupId, string? search, CancellationToken token = default)
    {
        return await repository.GetAvailableUsers(groupId, search, token);
    }

    /// <inheritdoc/>
    public async Task<GroupRole?> GetEffectiveGroupRoleAsync(ClaimsPrincipal user, int groupId)
    {
        if (user.IsInRole("SuperAdmin"))
            return GroupRole.Admin;

        return await GetGroupRoleAsync(user, groupId);
    }

    /// <inheritdoc/>
    public async Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId)
    {
        var userId = await identityService.GetUserIdAsync(user);
        if (userId == null) return null;
        return await repository.GetGroupRoleAsync(userId.Value, groupId);
    }

    /// <inheritdoc/>
    public async Task<GroupRole?> GetGroupRoleByIdAsync(int userId, int groupId)
    {
        return await repository.GetGroupRoleAsync(userId, groupId);
    }

    /// <inheritdoc/>
    public async Task<User> GetUserAsync(ClaimsPrincipal user)
    {
        var userId = await identityService.GetUserIdAsync(user);
        if (userId == null) return new User();
        return await repository.GetByIdAsync(userId.Value) ?? new User();
    }

    /// <inheritdoc/>
    public Task<SignInResult> PasswordSignInAsync(string email, string password, bool rememberMe, bool lockoutOnFailure) => identityService.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure);

    /// <inheritdoc/>
    public async Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword)
    {
        return await identityService.ResetPasswordAsync(user.Id, token, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ResetPasswordAsync(ClaimsPrincipal adminUser, int groupId, User user, string newPassword)
    {
        var reach = await GetAccountReachAsync(adminUser, groupId, [user.Id]);
        if (reach[user.Id] != AccountReach.Reachable)
        {
            return IdentityResult.Failed(new IdentityError { Description = "This account is outside the reach of the account making the change." });
        }

        return await identityService.AdminResetPasswordAsync(adminUser, user.Id, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, AccountReach>> GetAccountReachAsync(ClaimsPrincipal actor, int groupId, IReadOnlyCollection<int> targetUserIds, CancellationToken token = default)
    {
        var ids = targetUserIds.Distinct().ToList();
        var reach = ids.ToDictionary(id => id, _ => AccountReach.Reachable);

        if (ids.Count == 0 || actor.IsInRole("SuperAdmin"))
            return reach;

        var otherBoardAdministrators = await repository.GetUserIdsAdministeringOtherGroupsAsync(ids, groupId, token);
        foreach (var id in ids.Where(otherBoardAdministrators.Contains))
        {
            reach[id] = AccountReach.AdministersAnotherBoard;
        }

        // Platform administrator is the Identity role only, never a membership row, and outranks the other refusal.
        var superAdmins = await identityService.GetSuperAdminIdsAsync(ids, token);
        foreach (var id in ids.Where(superAdmins.Contains))
        {
            reach[id] = AccountReach.SuperAdminAccount;
        }

        // Changing one's own name, email or password never reaches beyond oneself.
        var actorId = await identityService.GetUserIdAsync(actor);
        if (actorId != null && reach.ContainsKey(actorId.Value))
            reach[actorId.Value] = AccountReach.Reachable;

        return reach;
    }

    /// <inheritdoc/>
    public async Task<string?> GeneratePasswordResetTokenForUserAsync(int userId)
    {
        return await identityService.GeneratePasswordResetTokenForUserAsync(userId);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId)
    {
        return await identityService.ConfirmEmailDirectlyAsync(userId);
    }

    /// <inheritdoc/>
    public async Task<bool> HasPasswordAsync(int userId)
    {
        return await identityService.HasPasswordAsync(userId);
    }

    /// <inheritdoc/>
    public async Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role)
    {
        return await repository.SetGroupRoleAsync(userId, groupId, role);
    }

    /// <inheritdoc/>
    public async Task<MemberChangeOutcome> ChangeMemberRoleAsync(int groupId, int actingUserId, int targetUserId, GroupRole newRole, CancellationToken token = default)
    {
        var currentRole = await repository.GetGroupRoleAsync(targetUserId, groupId);
        if (currentRole == null)
            return MemberChangeOutcome.NotAMember;

        if (actingUserId == targetUserId && currentRole != newRole)
            return MemberChangeOutcome.Self;

        if (currentRole == GroupRole.Admin && newRole != GroupRole.Admin)
        {
            var admins = await groupService.CountMembersWithRoleAsync(groupId, GroupRole.Admin, token);
            if (admins <= 1)
                return MemberChangeOutcome.LastAdmin;
        }

        // Update only: if the person was removed from the board after the read above, the change must not re-add them.
        return await repository.UpdateGroupRoleAsync(targetUserId, groupId, newRole, token)
            ? MemberChangeOutcome.Changed
            : MemberChangeOutcome.NotAMember;
    }

    /// <inheritdoc/>
    public async Task<MemberChangeOutcome> TryRemoveMemberAsync(int groupId, int actingUserId, int targetUserId, CancellationToken token = default)
    {
        var currentRole = await repository.GetGroupRoleAsync(targetUserId, groupId);
        if (currentRole == null)
            return MemberChangeOutcome.NotAMember;

        if (actingUserId == targetUserId)
            return MemberChangeOutcome.Self;

        if (currentRole == GroupRole.Admin)
        {
            var admins = await groupService.CountMembersWithRoleAsync(groupId, GroupRole.Admin, token);
            if (admins <= 1)
                return MemberChangeOutcome.LastAdmin;
        }

        await groupService.RemoveMemberAsync(groupId, targetUserId, token);
        return MemberChangeOutcome.Changed;
    }

    /// <inheritdoc/>
    public async Task<CreateOrAddToGroupResult> CreateOrAddToGroupAsync(string email, string name, int groupId, GroupRole role, CancellationToken token = default)
    {
        var userId = await identityService.GetIdByEmailAsync(email);

        if (userId == null)
        {
            var createResult = await CreateAsync(email, name);
            if (!createResult.Succeeded)
            {
                return new CreateOrAddToGroupResult
                {
                    Outcome = CreateOrAddToGroupOutcome.Failed,
                    Email = email,
                    Name = name,
                    Errors = createResult.Errors.Select(e => e.Description).ToList()
                };
            }

            var newUserId = await identityService.GetIdByEmailAsync(email);
            if (newUserId == null)
            {
                return new CreateOrAddToGroupResult
                {
                    Outcome = CreateOrAddToGroupOutcome.Failed,
                    Email = email,
                    Name = name,
                    Errors = ["Account was created but could not be re-resolved by email."]
                };
            }

            await SetGroupRoleAsync(newUserId.Value, groupId, role);

            return new CreateOrAddToGroupResult
            {
                Outcome = CreateOrAddToGroupOutcome.NewAccountCreated,
                UserId = newUserId,
                Email = email,
                Name = name
            };
        }

        // Existing account collision: the submitted name is never used to modify the
        // existing account, so load the real name/email/confirmation state instead.
        var existingUser = await GetByIdAsync(userId.Value, token);

        try
        {
            await groupService.AddMemberAsync(groupId, userId.Value, role, token);
        }
        catch (InvalidOperationException)
        {
            return new CreateOrAddToGroupResult
            {
                Outcome = CreateOrAddToGroupOutcome.AlreadyMember,
                UserId = userId,
                Email = existingUser?.Email ?? email,
                Name = existingUser?.Name ?? name
            };
        }

        var outcome = existingUser?.EmailConfirmed == false
            ? CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount
            : CreateOrAddToGroupOutcome.AddedToGroup;

        return new CreateOrAddToGroupResult
        {
            Outcome = outcome,
            UserId = userId,
            Email = existingUser?.Email ?? email,
            Name = existingUser?.Name ?? name
        };
    }

    /// <inheritdoc/>
    public Task SignOutAsync() => identityService.SignOutAsync();
}
