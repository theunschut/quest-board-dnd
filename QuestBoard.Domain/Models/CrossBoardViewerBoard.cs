using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

/// <summary>
/// One board the viewer belongs to, with just enough about it to decide whether a cross-board
/// link may take them there: the viewer's role on it, its type, and the pages it has switched
/// off. It never carries board content.
/// </summary>
public sealed record CrossBoardViewerBoard(int GroupId, GroupRole Role, BoardType BoardType, BoardPageSet DisabledPages);
