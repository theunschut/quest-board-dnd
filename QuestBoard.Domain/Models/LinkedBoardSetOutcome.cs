using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

/// <summary>
/// The outcome of a linked board set change, with the names a message needs.
/// </summary>
public record LinkedBoardSetOutcome(
    LinkedBoardSetResult Result,
    string? SetName = null,
    string? BoardName = null,
    BoardType? BoardType = null,
    BoardType? SetBoardType = null);
