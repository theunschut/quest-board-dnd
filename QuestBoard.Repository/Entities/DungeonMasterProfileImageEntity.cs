using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("DungeonMasterProfileImages")]
public class DungeonMasterProfileImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(DungeonMasterProfile))]
    public int Id { get; set; }

    [Required]
    public byte[] OriginalImageData { get; set; } = [];

    public byte[]? CroppedImageData { get; set; }

    public virtual DungeonMasterProfileEntity DungeonMasterProfile { get; set; } = null!;
}
