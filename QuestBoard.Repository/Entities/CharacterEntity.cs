using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Characters")]
public class CharacterEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 20)]
    public int Level { get; set; } = 1;

    [StringLength(500)]
    public string? SheetLink { get; set; }

    public int Status { get; set; } = 0; // CharacterStatus enum stored as int

    public int Role { get; set; } = 1; // CharacterRole enum stored as int (1 = Backup)

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(5000)]
    public string? Backstory { get; set; }

    [Required]
    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual UserEntity Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual CharacterImageEntity? ProfileImage { get; set; }

    public virtual ICollection<CharacterClassEntity> Classes { get; set; } = [];

    public virtual ICollection<PlayerSignupEntity> PlayerSignups { get; set; } = [];

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
