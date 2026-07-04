using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Abstracts ASP.NET Core Identity operations over user entities.
/// Implemented in the Repository layer where the concrete user entity is known.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Adds the user to the given ASP.NET Core Identity role.
    /// </summary>
    Task<IdentityResult> AddToRoleAsync(int userId, string role);

    /// <summary>
    /// Changes the currently signed-in user's password after verifying the old password.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal user, string oldPassword, string newPassword);

    /// <summary>
    /// Changes the given user's password after verifying the old password.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword);

    /// <summary>
    /// Creates a new Identity user with no password set and assigns the Player role.
    /// </summary>
    Task<IdentityResult> CreateUserAsync(string email, string name);

    /// <summary>
    /// Returns the ASP.NET Core Identity roles assigned to the user.
    /// </summary>
    Task<IList<string>> GetRolesAsync(int userId);

    /// <summary>
    /// Returns the numeric user Id for the given ClaimsPrincipal, or null if not resolvable.
    /// </summary>
    Task<int?> GetUserIdAsync(ClaimsPrincipal user);

    /// <summary>
    /// Returns whether the user holds the given ASP.NET Core Identity role.
    /// </summary>
    Task<bool> IsInRoleAsync(int userId, string role);

    /// <summary>
    /// Returns whether the currently signed-in ClaimsPrincipal holds the given ASP.NET Core Identity role.
    /// </summary>
    Task<bool> IsInRoleAsync(ClaimsPrincipal user, string role);

    /// <summary>
    /// Attempts to sign in with the given credentials, honoring lockout policy.
    /// </summary>
    Task<SignInResult> PasswordSignInAsync(string email, string password, bool rememberMe, bool lockoutOnFailure);

    /// <summary>
    /// Removes the user from the given ASP.NET Core Identity role.
    /// </summary>
    Task<IdentityResult> RemoveFromRoleAsync(int userId, string role);

    /// <summary>
    /// Resets the user's password using a previously issued reset token.
    /// </summary>
    Task<IdentityResult> ResetPasswordAsync(int userId, string token, string newPassword);

    /// <summary>
    /// Resets a target user's password on behalf of an admin, without requiring the old password.
    /// Fails if the calling ClaimsPrincipal is not in the Admin role.
    /// </summary>
    Task<IdentityResult> AdminResetPasswordAsync(ClaimsPrincipal adminUser, int targetUserId, string newPassword);

    /// <summary>
    /// Returns whether the user has a password set (false for admin-created accounts awaiting first login).
    /// </summary>
    Task<bool> HasPasswordAsync(int userId);

    /// <summary>
    /// Generates a password-reset token for the given user, used to build the Welcome/SetPassword email link.
    /// </summary>
    Task<string?> GeneratePasswordResetTokenForUserAsync(int userId);

    /// <summary>
    /// Marks the user's email as confirmed without requiring a confirmation token, for admin-triggered flows.
    /// </summary>
    Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId);

    /// <summary>
    /// Generates a token authorizing the user's email address to be changed to newEmail.
    /// </summary>
    Task<string?> GenerateChangeEmailTokenAsync(int userId, string newEmail);

    /// <summary>
    /// Changes the user's email (and username) using a previously issued change-email token.
    /// </summary>
    Task<IdentityResult> ChangeEmailAsync(int userId, string newEmail, string token);

    /// <summary>
    /// Returns the numeric user Id for the given email address, or null if no matching user exists.
    /// </summary>
    Task<int?> GetIdByEmailAsync(string email);

    /// <summary>
    /// Signs the current user out of their authentication session.
    /// </summary>
    Task SignOutAsync();

    /// <summary>
    /// Disables the user's account by setting a permanent lockout end and invalidating any already-issued auth cookie.
    /// </summary>
    Task<IdentityResult> DisableUserAsync(int userId);

    /// <summary>
    /// Re-enables a previously disabled account by clearing its lockout end.
    /// </summary>
    Task<IdentityResult> EnableUserAsync(int userId);

    /// <summary>
    /// Returns the user's current lockout end value, or null if the user is not found or has none set.
    /// </summary>
    Task<DateTimeOffset?> GetLockoutEndAsync(int userId);
}
