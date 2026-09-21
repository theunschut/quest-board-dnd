namespace QuestBoard.Domain.Models;

/// <summary>
/// The board a cross-board link resolved to. The name is carried alongside the id deliberately:
/// the resolver already holds the viewer's own membership list when it builds this, which is
/// where a board name may legitimately be read from -- the membership-pinned lookup itself must
/// never return anything but an id.
/// </summary>
public sealed record CrossBoardTarget(int GroupId, string GroupName);
