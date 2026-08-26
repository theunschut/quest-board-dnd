using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.EventViewModels;

public class EventViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Event title is required")]
    [StringLength(200, ErrorMessage = "Event title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    // Unbounded Markdown, matching a quest description rather than a contact description.
    public string? Description { get; set; }

    [Required(ErrorMessage = "Event date is required")]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    // A null start time means the event runs all day.
    [DataType(DataType.Time)]
    public TimeOnly? StartTime { get; set; }

    // Display-only flag for the Dungeon Master action buttons. There is no owner concept for
    // an event, so any Dungeon Master on the board sees them; the authorization policy on the
    // write actions is the actual security boundary, not this flag.
    public bool CanManage { get; set; }

    // The single place that decides how an event's time is worded, so both the desktop chip
    // and the mobile agenda entry read the same and neither ever renders a blank time slot.
    public string TimeLabel => StartTime.HasValue ? StartTime.Value.ToString("HH:mm") : "All day";
}
