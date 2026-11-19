namespace QuestBoard.Domain.Enums;

/// <summary>
/// The outcome of a guarded change to a board member's role.
/// </summary>
public enum MemberChangeOutcome
{
    /// <summary>The change was made.</summary>
    Changed,

    /// <summary>The person is not a member of the board, so nothing was changed.</summary>
    NotAMember,

    /// <summary>The person asking is the person being changed, so nothing was changed.</summary>
    Self,

    /// <summary>The change would leave the board with no Admin, so nothing was changed.</summary>
    LastAdmin
}
