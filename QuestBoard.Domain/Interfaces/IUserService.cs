using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace QuestBoard.Domain.Interfaces;

public interface IUserService : IBaseService<User>
{
    /// <summary>
    /// Adds the user to the given ASP.NET Core Identity role.
    /// </summary>
    Task<IdentityResult> AddToRoleAsync(User user, string role);

    /// <summary>
    /// Changes the currently signed-in user's password after verifying the old password.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal user, string oldPassword, string newPassword);

    /// <summary>
    /// Changes the given user's password after verifying the old password.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword);

    /// <summary>
    /// Creates a new user account with no password set and assigns the Player role.
    /// The user must complete the Welcome/SetPassword flow before they can sign in.
    /// </summary>
    Task<IdentityResult> CreateAsync(string email, string name);

    /// <summary>
    /// Returns whether a user with the given name already exists (case-insensitive).
    /// </summary>
    Task<bool> ExistsAsync(string name);

    /// <summary>
    /// Returns all users holding the DungeonMaster or Admin group role in the active group.
    /// </summary>
    Task<IList<User>> GetAllDungeonMastersAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all users holding the Player group role in the active group.
    /// </summary>
    Task<IList<User>> GetAllPlayersAsync(CancellationToken token = default);

    /// <summary>
    /// Returns the effective GroupRole for the given ClaimsPrincipal in the specified group,
    /// treating SuperAdmin as an automatic Admin-equivalent bypass that requires no group membership.
    /// </summary>
    Task<GroupRole?> GetEffectiveGroupRoleAsync(ClaimsPrincipal user, int groupId);

    /// <summary>
    /// Returns the given ClaimsPrincipal's group role in the specified group, or null if they are not a member.
    /// </summary>
    Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId);

    /// <summary>
    /// Returns the given user's group role in the specified group, or null if they are not a member.
    /// </summary>
    Task<GroupRole?> GetGroupRoleByIdAsync(int userId, int groupId);

    /// <summary>
    /// Returns the ASP.NET Core Identity roles assigned to the user.
    /// </summary>
    Task<IList<string>> GetRolesAsync(User user);

    /// <summary>
    /// Returns the domain User for the currently signed-in ClaimsPrincipal, or a new empty User if not resolvable.
    /// </summary>
    Task<User> GetUserAsync(ClaimsPrincipal user);

    /// <summary>
    /// Returns whether the user holds the given ASP.NET Core Identity role.
    /// </summary>
    Task<bool> IsInRoleAsync(User user, string role);

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
    Task<IdentityResult> RemoveFromRoleAsync(User user, string role);

    /// <summary>
    /// Resets the user's password using a previously issued reset token.
    /// </summary>
    Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword);

    /// <summary>
    /// Resets a target user's password on behalf of an admin, without requiring the old password.
    /// Fails if the calling ClaimsPrincipal is not in the Admin role.
    /// </summary>
    Task<IdentityResult> ResetPasswordAsync(ClaimsPrincipal adminUser, User user, string newPassword);

    /// <summary>
    /// Generates a password-reset token for the given user, used to build the Welcome/SetPassword email link.
    /// </summary>
    Task<string?> GeneratePasswordResetTokenForUserAsync(int userId);

    /// <summary>
    /// Marks the user's email as confirmed without requiring a confirmation token, for admin-triggered flows.
    /// </summary>
    Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId);

    /// <summary>
    /// Returns whether the user has a password set (false for admin-created accounts awaiting first login).
    /// </summary>
    Task<bool> HasPasswordAsync(int userId);

    /// <summary>
    /// Creates or updates the user's role within the specified group. Returns the UserGroup row Id.
    /// </summary>
    Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role);

    /// <summary>
    /// Signs the current user out of their authentication session.
    /// </summary>
    Task SignOutAsync();
}
