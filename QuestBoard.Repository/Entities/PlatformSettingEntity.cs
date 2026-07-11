using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("PlatformSettings")]
public class PlatformSettingEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [StringLength(500)]
    public string Value { get; set; } = string.Empty;

    // Null means this is the instance-wide default row for the key; a non-null value
    // means this row is that group's own override for the key.
    public int? GroupId { get; set; }

    public virtual GroupEntity? Group { get; set; }
}
