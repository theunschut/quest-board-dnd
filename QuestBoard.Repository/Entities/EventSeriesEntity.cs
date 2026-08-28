using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

// This table stores the repeating schedule for a calendar event series, plus the template
// every generated occurrence is stamped from - its title, description and start time.
[Table("EventSeries")]
public class EventSeriesEntity : IEntity
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

    // A null start time means every generated occurrence runs all day.
    public TimeOnly? StartTime { get; set; }

    public DateOnly AnchorDate { get; set; }

    public int IntervalWeeks { get; set; }

    // Stores a day-of-week ordinal where 0 is Sunday, matching System.DayOfWeek. Derived from
    // AnchorDate and written on save rather than being independently editable - under the slot
    // arithmetic every generated date lands on the anchor's own weekday, so an independently-set
    // value could only ever be wrong.
    [Range(0, 6)]
    public int WeekDay { get; set; }

    [StringLength(200)]
    public string CycleMask { get; set; } = string.Empty;

    // A null end date means the series is open-ended and keeps generating occurrences
    // indefinitely.
    public DateOnly? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
