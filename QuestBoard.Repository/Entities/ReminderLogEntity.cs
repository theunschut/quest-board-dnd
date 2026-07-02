using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ReminderLogs")]
public class ReminderLogEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuestId { get; set; }

    [Required]
    public int PlayerId { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(QuestId))]
    public virtual QuestEntity Quest { get; set; } = null!;

    [ForeignKey(nameof(PlayerId))]
    public virtual UserEntity Player { get; set; } = null!;
}
