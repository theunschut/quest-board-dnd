namespace QuestBoard.Domain.Models;

/// <summary>
/// Describes which of the collision-aware outcomes occurred when creating or
/// adding a user to a group by email.
/// </summary>
public enum CreateOrAddToGroupOutcome
{
    /// <summary>
    /// The email did not belong to any existing account; a brand-new account was created
    /// and added to the group.
    /// </summary>
    NewAccountCreated,

    /// <summary>
    /// The email belonged to an existing, already-confirmed account that was not yet a
    /// member of the group; the account was added to the group.
    /// </summary>
    AddedToGroup,

    /// <summary>
    /// The email belonged to an existing account that never completed its own onboarding
    /// (no confirmed email / no password set) and was not yet a member of the group; the
    /// account was added to the group.
    /// </summary>
    AddedToGroupStrandedAccount,

    /// <summary>
    /// The email belonged to an existing account that was already a member of the group;
    /// no membership row was created.
    /// </summary>
    AlreadyMember,

    /// <summary>
    /// A brand-new account creation attempt failed; see <see cref="CreateOrAddToGroupResult.Errors"/>.
    /// </summary>
    Failed
}

/// <summary>
/// The result of a collision-aware create-or-add-to-group operation, carrying the
/// resolved outcome plus enough user information for the caller to send the right
/// notification email or flash message.
/// </summary>
public record CreateOrAddToGroupResult
{
    public required CreateOrAddToGroupOutcome Outcome { get; init; }

    /// <summary>
    /// The resolved user's Id. Null only when a brand-new account creation failed.
    /// </summary>
    public int? UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Error descriptions from a failed account creation attempt. Empty for every
    /// outcome except <see cref="CreateOrAddToGroupOutcome.Failed"/>.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
