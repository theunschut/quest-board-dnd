namespace QuestBoard.Domain.Enums;

/// <summary>
/// The result of a linked board set change, so a caller can tell a success from each kind of refusal.
/// </summary>
public enum LinkedBoardSetResult
{
    Success,
    NotFound,
    InvalidName,
    InvalidBoardType,
    DuplicateName,
    TypeMismatch,
    AlreadyLinked,
    NotInSet
}
