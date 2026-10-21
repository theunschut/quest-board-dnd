using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

/// <summary>
/// Another board in the active board's linked set, with the viewer's role on it. The role is
/// null when the viewer is not a member of that board.
/// </summary>
public class LinkedBoard
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public GroupRole? ViewerRole { get; set; }

    public bool ViewerIsMember => ViewerRole != null;

    /// <summary>The pages this board has switched off; the empty set means it offers everything.</summary>
    public BoardPageSet DisabledPages { get; set; }
}
