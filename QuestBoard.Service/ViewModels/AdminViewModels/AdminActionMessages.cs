namespace QuestBoard.Service.ViewModels.AdminViewModels;

/// <summary>
/// The sentences the board Users page and the platform board pages show after an account action is refused or done,
/// kept in one place so a refusal reads the same wherever it is raised.
/// </summary>
public static class AdminActionMessages
{
    /// <summary>Shown when the account is one only a platform administrator may change.</summary>
    public const string ReachRefused = "Only a SuperAdmin can change that account.";

    /// <summary>Shown after a password was reset for the named account.</summary>
    public static string PasswordReset(string name) => $"Password reset successfully for {name}!";
}
