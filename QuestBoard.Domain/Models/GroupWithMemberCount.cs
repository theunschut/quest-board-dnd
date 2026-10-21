using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class GroupWithMemberCount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public BoardType BoardType { get; set; }

    /// <summary>The page the board opens on, as stored on the board's row.</summary>
    public BoardPage HomePage { get; set; }
}
