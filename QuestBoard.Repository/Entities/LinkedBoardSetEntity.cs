using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("LinkedBoardSets")]
public class LinkedBoardSetEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // The kind of board this set links, stored as the BoardType value. A set only ever holds
    // boards of one type, so an empty set can still refuse a board of the wrong type.
    public int BoardType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GroupEntity> Boards { get; set; } = [];
}
