using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("CharacterClasses")]
public class CharacterClassEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CharacterId { get; set; }

    [ForeignKey(nameof(CharacterId))]
    public virtual CharacterEntity Character { get; set; } = null!;

    public int Class { get; set; } // DndClass enum stored as int

    [Range(1, 20)]
    public int ClassLevel { get; set; }
}
