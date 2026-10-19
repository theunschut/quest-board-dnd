using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// One person named in a move preview. Events carry no signup role and no selection.
/// </summary>
public record MovePerson(int UserId, string Name, SignupRole? Role, bool IsSelected);
