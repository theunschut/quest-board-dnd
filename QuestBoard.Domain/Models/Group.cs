using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class Group : IModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public BoardType BoardType { get; set; }

    /// <summary>The pages this board has switched off. Empty means every page its type allows is offered.</summary>
    public BoardPageSet DisabledPages { get; set; }

    /// <summary>The page the board opens on, before the policy falls back to one that is available.</summary>
    public BoardPage HomePage { get; set; }
}
