using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("CalendarSubscriptions")]
public class CalendarSubscriptionEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual UserEntity User { get; set; } = null!;

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Token { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastFetchedAt { get; set; }

    // A retired subscription is a tombstone, mirroring EventEntity.CancelledAt: null means the
    // address is live, a value means it was retired at that time. The row survives a revoke so
    // the feed endpoint can keep answering a retired address differently from one that never
    // existed. A separate, bounded sweep is responsible for eventually purging a row that has
    // stayed retired long enough -- this table's own request path never hard-deletes a row.
    public DateTime? RevokedAt { get; set; }
}
