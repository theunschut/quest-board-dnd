using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Events")]
public class EventEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    // Unbounded Markdown, rendered through the shared markdown service - deliberately
    // unbounded like a quest description rather than length-limited like a contact note.
    public string? Description { get; set; }

    public DateOnly Date { get; set; }

    // A null start time means the event runs all day.
    public TimeOnly? StartTime { get; set; }

    [ForeignKey(nameof(SeriesId))]
    public virtual EventSeriesEntity? Series { get; set; }

    public int? SeriesId { get; set; }

    // Identifies which slot of a repeating schedule produced this occurrence; stays null
    // for a one-off event.
    public int? SeriesSlotIndex { get; set; }

    // A cancelled occurrence is a tombstone: its row and its availability answers survive,
    // but every read surface that renders events must account for it. Null means the
    // occurrence is live; un-cancelling is a single write of null with no data loss.
    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;

    // Exists so an event and its automatic board-wide signups can be written in a single
    // save. The matching domain model deliberately has no counterpart, so mapping a domain
    // model onto a tracked entity can never replace this navigation.
    public virtual ICollection<EventSignupEntity> Signups { get; set; } = [];

    // There is deliberately no author column (no CreatedByUserId, no DungeonMasterId): an
    // event is board-level information rather than one person's item, so any Dungeon Master
    // on the board may edit or delete it.

    // There is deliberately no category or kind discriminator and no relationship of any
    // sort to a quest: an event's meaning comes from the board it lives on rather than from
    // a category field, and events are informational and are deliberately not linked to quests.
}
