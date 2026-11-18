namespace QuestBoard.Domain.Enums;

/// <summary>
/// How far an account sits from a board Admin's reach: whether a person administering one board may reset the password of,
/// or edit, a given account from that board's Users page.
/// </summary>
public enum AccountReach
{
    /// <summary>The account may be changed from the active board.</summary>
    Reachable = 0,

    /// <summary>The account is a platform administrator (the Identity SuperAdmin role), which only another one may change.</summary>
    SuperAdminAccount = 1,

    /// <summary>The account administers a board other than the active one, so changing it reaches beyond this board.</summary>
    AdministersAnotherBoard = 2
}
