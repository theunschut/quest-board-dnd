using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("DungeonMasterProfiles")]
public class DungeonMasterProfileEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]  // Id = UserId, NOT auto-generated
    public int Id { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    public virtual DungeonMasterProfileImageEntity? ProfileImage { get; set; }
}
