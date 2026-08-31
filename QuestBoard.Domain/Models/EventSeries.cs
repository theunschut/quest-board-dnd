using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class EventSeries : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    // Unbounded Markdown, rendered through the shared markdown service.
    public string? Description { get; set; }

    public DateOnly AnchorDate { get; set; }

    // A null start time means every generated occurrence runs all day.
    public TimeOnly? StartTime { get; set; }

    public int IntervalWeeks { get; set; }

    // Stores a day-of-week ordinal where 0 is Sunday, matching System.DayOfWeek. Derived from
    // AnchorDate rather than being independently editable.
    public int WeekDay { get; set; }

    // The comma-delimited storage form of the recurrence cycle.
    public string CycleMask { get; set; } = string.Empty;

    // A null end date means the series is open-ended and keeps generating occurrences
    // indefinitely.
    public DateOnly? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }
}
