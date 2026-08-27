using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

// This table carries no GroupId of its own and is tenant-scoped through its required
// Event navigation. No code reads or writes it yet.
[Table("EventSignups")]
public class EventSignupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual EventEntity Event { get; set; } = null!;

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual UserEntity User { get; set; } = null!;

    // Stores the same three availability values used for quest date votes, where 0 is No,
    // 1 is Maybe and 2 is Yes.
    [Range(0, 2)]
    public int Availability { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // A null value means no person has ever set this answer - the row was created by an
    // automatic signup pass. Any non-null value means a person deliberately chose this
    // answer, including when they chose it on the click that created the row.
    public DateTime? UpdatedAt { get; set; }
}
