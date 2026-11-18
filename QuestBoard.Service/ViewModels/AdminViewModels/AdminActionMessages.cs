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

    /// <summary>Shown when one administrator has created accounts faster than the allowed pace.</summary>
    public const string CreateLimited = "You have created many accounts in a short time. Try again later.";

    /// <summary>Shown after a password was reset for the named account.</summary>
    public static string PasswordReset(string name) => $"Password reset successfully for {name}!";

    /// <summary>Shown after a member was removed from the board; the account itself and its other boards are untouched.</summary>
    public static string MemberRemoved(string name) => $"{name} was removed from the group.";

    /// <summary>Shown when the person to remove is no longer a member of the board.</summary>
    public const string MemberStale = "That person is not a member of this board any more.";

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
}
