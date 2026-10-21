using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Groups")]
public class GroupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BoardType { get; set; }

    /// <summary>Bit mask of switched-off pages; bit (1 shifted by the page value) set means off. Zero means nothing is off.</summary>
    public int DisabledPages { get; set; }

    /// <summary>The stored home page value; zero is the quest list.</summary>
    public int HomePage { get; set; }

    public int? LinkedBoardSetId { get; set; }

    [ForeignKey(nameof(LinkedBoardSetId))]
    public virtual LinkedBoardSetEntity? LinkedBoardSet { get; set; }

    public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
}
