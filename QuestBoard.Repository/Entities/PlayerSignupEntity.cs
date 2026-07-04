using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("PlayerSignups")]
public class PlayerSignupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int SignupRole { get; set; } = 0; // 0 = Player (default)

    [ForeignKey(nameof(QuestId))]
    public bool IsSelected { get; set; }

    [Required]
    public int PlayerId { get; set; }

    public DateTime SignupTime { get; set; } = DateTime.UtcNow;

    // Ordering timestamp reset on every post-finalization vote change; null means the vote
    // has never changed since signup.
    public DateTime? LastVoteChangeTime { get; set; }

    [Required]
    public int QuestId { get; set; }

    public int? CharacterId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public UserEntity Player { get; set; } = null!;

    [ForeignKey(nameof(QuestId))]
    public virtual QuestEntity Quest { get; set; } = null!;

    [ForeignKey(nameof(CharacterId))]
    public virtual CharacterEntity? Character { get; set; }

    public virtual ICollection<PlayerDateVoteEntity> DateVotes { get; set; } = [];
}