using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class Character : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public byte[]? ProfilePicture { get; set; }

    [Range(1, 20)]
    public int Level { get; set; } = 1;

    [StringLength(500)]
    public string? SheetLink { get; set; }

    public CharacterStatus Status { get; set; } = CharacterStatus.Active;

    public CharacterRole Role { get; set; } = CharacterRole.Backup;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(5000)]
    public string? Backstory { get; set; }

    public int OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Multi-class support: each character can have multiple classes with level distribution
    public IList<CharacterClass> Classes { get; set; } = [];
}

public class CharacterClass : IModel
{
    public int Id { get; set; }

    public int CharacterId { get; set; }

    public Character Character { get; set; } = null!;

    public DndClass Class { get; set; }

    [Range(1, 20)]
    public int ClassLevel { get; set; }
}
