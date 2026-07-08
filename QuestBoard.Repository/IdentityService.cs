using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace QuestBoard.Repository;

internal class IdentityService(UserManager<UserEntity> userManager, SignInManager<UserEntity> signInManager) : IIdentityService
{
    /// <inheritdoc/>
    public async Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal user, string oldPassword, string newPassword)
    {
        var entity = await userManager.GetUserAsync(user);
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });
        return await userManager.ChangePasswordAsync(entity, oldPassword, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });
        return await userManager.ChangePasswordAsync(entity, oldPassword, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> CreateUserAsync(string email, string name)
    {
        var entity = new UserEntity
        {
            UserName = email,
            Email = email,
            Name = name
        };
        var result = await userManager.CreateAsync(entity);

        // Account is created without a password (PasswordHash stays null) and with no role —
        // the user must complete the Welcome/SetPassword flow before they can sign in, and
        // per-group role assignment happens later via group membership.

        return result;
    }

    /// <inheritdoc/>
    public async Task<int?> GetUserIdAsync(ClaimsPrincipal user)
    {
        var entity = await userManager.GetUserAsync(user);
        return entity?.Id;
    }

    /// <inheritdoc/>
    public Task<SignInResult> PasswordSignInAsync(string email, string password, bool rememberMe, bool lockoutOnFailure)
        => signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure);

    /// <inheritdoc/>
    public async Task<IdentityResult> ResetPasswordAsync(int userId, string token, string newPassword)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });
        return await userManager.ResetPasswordAsync(entity, token, newPassword);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> AdminResetPasswordAsync(ClaimsPrincipal adminUser, int targetUserId, string newPassword)
    {
        var adminEntity = await userManager.GetUserAsync(adminUser);
        if (adminEntity == null)
            return IdentityResult.Failed(new IdentityError { Description = "Admin user not found or not authorized." });

        var entity = await userManager.FindByIdAsync(targetUserId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(entity);
        return await userManager.ResetPasswordAsync(entity, resetToken, newPassword);
    }

    /// <inheritdoc/>
    public async Task<string?> GeneratePasswordResetTokenForUserAsync(int userId)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null) return null;
        return await userManager.GeneratePasswordResetTokenAsync(entity);
    }

    /// <inheritdoc/>
    public async Task<bool> HasPasswordAsync(int userId)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null) return false;
        return await userManager.HasPasswordAsync(entity);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        entity.EmailConfirmed = true;
        return await userManager.UpdateAsync(entity);
    }

    /// <inheritdoc/>
    public async Task<int?> GetIdByEmailAsync(string email)
    {
        var entity = await userManager.FindByEmailAsync(email);
        return entity?.Id;
    }

    /// <inheritdoc/>
    public async Task<string?> GenerateChangeEmailTokenAsync(int userId, string newEmail)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null) return null;
        return await userManager.GenerateChangeEmailTokenAsync(entity, newEmail);
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> ChangeEmailAsync(int userId, string newEmail, string token)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        var result = await userManager.ChangeEmailAsync(entity, newEmail, token);
        if (result.Succeeded)
            await userManager.SetUserNameAsync(entity, newEmail);

        return result;
    }

    /// <inheritdoc/>
    public Task SignOutAsync() => signInManager.SignOutAsync();

    /// <inheritdoc/>
    public async Task<IdentityResult> DisableUserAsync(int userId)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        await userManager.SetLockoutEndDateAsync(entity, DateTimeOffset.MaxValue);
        // Bump the security stamp so any already-issued auth cookie for this account is invalidated on next re-validation.
        await userManager.UpdateSecurityStampAsync(entity);
        return IdentityResult.Success;
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> EnableUserAsync(int userId)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });

        // No security stamp bump needed here — a disabled user has no active session to invalidate,
        // since they were already blocked from signing in.
        await userManager.SetLockoutEndDateAsync(entity, null);
        return IdentityResult.Success;
    }

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetLockoutEndAsync(int userId)
    {
        var entity = await userManager.FindByIdAsync(userId.ToString());
        return entity?.LockoutEnd;
    }
}
