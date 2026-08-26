using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class Event : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    // Unbounded Markdown, rendered through the shared markdown service.
    public string? Description { get; set; }

    public DateOnly Date { get; set; }

    // A null start time means the event runs all day.
    public TimeOnly? StartTime { get; set; }

    public int? SeriesId { get; set; }

    // Identifies which slot of a repeating schedule produced this occurrence; stays null
    // for a one-off event.
    public int? SeriesSlotIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }
}
