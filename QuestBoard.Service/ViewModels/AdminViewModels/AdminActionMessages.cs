using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

/// <summary>
/// The sentences the board Users page and the platform board pages show after an account action is refused or done,
/// kept in one place so a refusal reads the same wherever it is raised.
/// </summary>
public static class AdminActionMessages
{
    /// <summary>Shown when the account is one only a platform administrator may change.</summary>
    public const string ReachRefused = "Only a SuperAdmin can change that account.";

    /// <summary>Shown when an account has been sent its hourly share of emails and a further send is refused.</summary>
    public const string EmailRateLimited = "Too many emails were sent to this account recently. Try again in an hour.";

    /// <summary>The Error toast shown when the Create New User dialog's post is invalid or the account could not be made.</summary>
    public const string CreateMemberFailed = "The account could not be created. Check the details and try again.";

    /// <summary>Shown when one administrator has created accounts faster than the allowed pace.</summary>
    public const string CreateLimited = "You have created many accounts in a short time. Try again later.";

    /// <summary>Shown after a password was reset for the named account.</summary>
    public static string PasswordReset(string name) => $"Password reset successfully for {name}!";

    /// <summary>
    /// Shown after an existing account was added to the board and told so by email. The role is the displayed word
    /// ("Dungeon Master"), never the enum name.
    /// </summary>
    public static string AddedToGroup(string name, GroupRole role) =>
        $"{name} has been added to the group as {AdminPlatformDisplay.GroupRoleName(role)}. A notification email has been sent.";

    /// <summary>Shown after a member was removed from the board; the account itself and its other boards are untouched.</summary>
    public static string MemberRemoved(string name) => $"{name} was removed from the group.";

    /// <summary>Shown when the person to remove is no longer a member of the board.</summary>
    public const string MemberStale = "That person is not a member of this board any more.";

    /// <summary>Shown on the platform board page when the person to remove was not a member of the board.</summary>
    public static string NotAMemberOfGroup(string name) => $"{name} was not a member of this group.";

    /// <summary>Shown when an administrator tries to change their own role or remove themselves from the board.</summary>
    public const string SelfRefused = "You cannot change your own role or remove yourself from the board.";

    /// <summary>Shown when a change would leave the board with nobody who can manage it.</summary>
    public const string LastAdminRefused = "A board must keep at least one member who can manage it. Give someone else that role first.";

    /// <summary>Shown after a quest was deleted from the board.</summary>
    public const string QuestDeleted = "Quest deleted.";

    /// <summary>Shown when the quest to delete is gone, or belongs to another board.</summary>
    public const string QuestMissing = "That quest no longer exists on this board.";

    /// <summary>Shown when the database refused the delete and left the quest as it was.</summary>
    public const string QuestRefused = "That quest could not be deleted. Nothing was changed.";

    /// <summary>The reason shown beside the controls refused on the signed-in person's own row of the Users page.</summary>
    public const string ReasonSelf = "This is your own account, so you cannot change your role or remove yourself here.";

    /// <summary>The reason shown beside the controls refused on the row of the board's only Admin.</summary>
    public const string ReasonLastAdmin = "This person is the only one who can manage this board. Give someone else that role first.";

    /// <summary>The reason shown beside the controls refused on an account only a platform administrator may change.</summary>
    public const string ReasonReach = "Only a SuperAdmin can change this account.";

    /// <summary>The question asked before a member is removed from the board.</summary>
    public const string RemoveMemberConfirm = "Are you sure you want to remove this user from the group? Their availability answers for events on this board will be deleted and cannot be recovered. They will keep their account and any other group memberships.";
}
