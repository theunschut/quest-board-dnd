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
    public async Task<IdentityResult> AddToRoleAsync(User user, string role)
    {
        return await identityService.AddToRoleAsync(user.Id, role);
    }

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
    public async Task<IList<string>> GetRolesAsync(User user)
    {
        return await identityService.GetRolesAsync(user.Id);
    }

    /// <inheritdoc/>
    public async Task<User> GetUserAsync(ClaimsPrincipal user)
    {
        var userId = await identityService.GetUserIdAsync(user);
        if (userId == null) return new User();
        return await repository.GetByIdAsync(userId.Value) ?? new User();
    }

    /// <inheritdoc/>
    public async Task<bool> IsInRoleAsync(User user, string role)
    {
        return await identityService.IsInRoleAsync(user.Id, role);
    }

    /// <inheritdoc/>
    public async Task<bool> IsInRoleAsync(ClaimsPrincipal user, string role)
    {
        return await identityService.IsInRoleAsync(user, role);
    }

    /// <inheritdoc/>
    public Task<SignInResult> PasswordSignInAsync(string email, string password, bool rememberMe, bool lockoutOnFailure) => identityService.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure);

    /// <inheritdoc/>
    public async Task<IdentityResult> RemoveFromRoleAsync(User user, string role)
    {
        return await identityService.RemoveFromRoleAsync(user.Id, role);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword)
    {
        return await identityService.ResetPasswordAsync(user.Id, token, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ResetPasswordAsync(ClaimsPrincipal adminUser, User user, string newPassword)
    {
        return await identityService.AdminResetPasswordAsync(adminUser, user.Id, newPassword);
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
