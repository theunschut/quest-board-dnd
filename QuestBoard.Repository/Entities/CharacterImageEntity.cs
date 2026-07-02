using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("CharacterImages")]
public class CharacterImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(Character))]
    public int Id { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = [];

    public virtual CharacterEntity Character { get; set; } = null!;
}