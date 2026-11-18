using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

/// <summary>
/// What the shared quest status badge needs: the quest, the type of the board it sits on, and the board's own today.
/// The page is handed the day by its controller, so the badge never reads a clock itself.
/// </summary>
/// <param name="Quest">The quest whose status is shown.</param>
/// <param name="BoardType">The type of the active board.</param>
/// <param name="BoardToday">The board's own calendar day.</param>
public sealed record QuestStatusBadgeModel(Quest Quest, BoardType BoardType, DateOnly BoardToday);
