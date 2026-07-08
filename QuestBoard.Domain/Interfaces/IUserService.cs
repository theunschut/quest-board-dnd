using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace QuestBoard.Domain.Interfaces;

public interface IUserService : IBaseService<User>
{
    /// <summary>
    /// Changes the currently signed-in user's password after verifying the old password.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal user, string oldPassword, string newPassword);

    /// <summary>
    /// Changes the given user's password after verifying the old password.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword);

    /// <summary>
    /// Creates a new user account with no password set and no role assigned; per-group roles
    /// are assigned later via group membership. The user must complete the Welcome/SetPassword
    /// flow before they can sign in.
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
    /// Returns all members of the specified group, regardless of their group role.
    /// </summary>
    Task<IList<User>> GetAllGroupMembersAsync(int groupId, CancellationToken token = default);

    /// <summary>
    /// Returns users who are NOT members of the given group, optionally filtered by a search term
    /// matching Name or Email (case-insensitive).
    /// </summary>
    Task<IList<User>> GetAvailableUsersAsync(int groupId, string? search, CancellationToken token = default);

    /// <summary>
    /// Returns the effective GroupRole for the given ClaimsPrincipal in the specified group,
    /// treating SuperAdmin as an automatic Admin-equivalent bypass that requires no group membership.
    /// </summary>
    Task<GroupRole?> GetEffectiveGroupRoleAsync(ClaimsPrincipal user, int groupId);

    /// <summary>
    /// Returns the given ClaimsPrincipal's raw group role in the specified group, or null if they are not a member.
    /// </summary>
    /// <remarks>
    /// This does NOT apply the SuperAdmin bypass — a SuperAdmin with no membership row in the
    /// group returns null here even though they should be treated as an Admin-equivalent for
    /// authorization purposes. Callers making an authorization decision should use
    /// <see cref="GetEffectiveGroupRoleAsync"/> instead, or implement their own explicit
    /// SuperAdmin short-circuit before falling back to this method (as the DM/Admin authorization
    /// handlers do).
    /// </remarks>
    Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId);

    /// <summary>
    /// Returns the given user's group role in the specified group, or null if they are not a member.
    /// </summary>
    Task<GroupRole?> GetGroupRoleByIdAsync(int userId, int groupId);

    /// <summary>
    /// Returns the domain User for the currently signed-in ClaimsPrincipal, or a new empty User if not resolvable.
    /// </summary>
    Task<User> GetUserAsync(ClaimsPrincipal user);

    /// <summary>
    /// Attempts to sign in with the given credentials, honoring lockout policy.
    /// </summary>
    Task<SignInResult> PasswordSignInAsync(string email, string password, bool rememberMe, bool lockoutOnFailure);

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
    /// Collision-aware user creation: creates a brand-new account when the email is unused,
    /// or adds the existing account to the group when the email already belongs to a user.
    /// </summary>
    /// <remarks>
    /// Returns one of four outcomes: a new account was created; an existing, already-confirmed
    /// account was added to the group; an existing account that never completed its own
    /// onboarding was added to the group; or the email already belonged to a member of the
    /// group, in which case no membership row is created. On any collision branch the
    /// submitted name is ignored — the existing account's name is never modified.
    /// </remarks>
    Task<CreateOrAddToGroupResult> CreateOrAddToGroupAsync(string email, string name, int groupId, GroupRole role, CancellationToken token = default);

    /// <summary>
    /// Signs the current user out of their authentication session.
    /// </summary>
    Task SignOutAsync();
}
