using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ProposedDates")]
public class ProposedDateEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuestId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [ForeignKey(nameof(QuestId))]
    public virtual QuestEntity Quest { get; set; } = null!;

    public virtual ICollection<PlayerDateVoteEntity> PlayerVotes { get; set; } = [];
}