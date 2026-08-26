using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

// This table stores the repeating-schedule definition for a calendar event series.
// No code reads or writes it yet - it exists now so the storage convention and tenant
// scoping are settled before any occurrence data is created.
[Table("EventSeries")]
public class EventSeriesEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public DateOnly AnchorDate { get; set; }

    public int IntervalWeeks { get; set; }

    // Stores a day-of-week ordinal where 0 is Sunday, matching System.DayOfWeek.
    [Range(0, 6)]
    public int WeekDay { get; set; }

    [StringLength(200)]
    public string CycleMask { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
