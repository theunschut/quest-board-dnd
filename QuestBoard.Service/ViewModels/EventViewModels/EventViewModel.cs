using System.ComponentModel.DataAnnotations;
using QuestBoard.Domain.Enums;

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

    // Every signup row on the event, name-ordered by the repository so the view never has to
    // sort it itself.
    public IList<EventSignupViewModel> Roster { get; set; } = [];

    // Display flag deciding whether the withdraw control renders. It only controls rendering:
    // the controller re-checks the board type on the write itself, because hidden markup is
    // never an enforcement mechanism on its own.
    public bool IsOneShotBoard { get; set; }

    // Whether the viewer holds a row at all, so the view can tell "not answered" apart from
    // an actual answer rather than guessing from a default value.
    public bool HasOwnSignup { get; set; }

    // The viewer's own current answer; null when they hold no row.
    public VoteType? MyAvailability { get; set; }

    // Computed, not settable. Counts every signup row on the event including the automatic
    // ones created by campaign membership, deliberately -- this is what the delete
    // confirmation reports to the acting Dungeon Master.
    public int SignupCount => Roster.Count;

    // The four recurrence inputs below are read only when the repeats toggle is on and are
    // never round-tripped for a one-off event -- the mapping profile ignores them on the way
    // back out of the domain model.
    public bool IsRecurring { get; set; }

    [Range(1, 52, ErrorMessage = "Repeat every must be between 1 and 52 weeks")]
    public int IntervalWeeks { get; set; } = 1;

    public string CycleMask { get; set; } = "1";

    [DataType(DataType.Date)]
    public DateOnly? SeriesEndDate { get; set; }

    // Display and scope members used by the details, edit and series surfaces.
    public int? SeriesId { get; set; }

    public DateTime? CancelledAt { get; set; }

    // Computed rather than stored on the view model, so a form post can never set it.
    public bool IsCancelled => CancelledAt != null;

    // The hidden field the save-scope dialog sets; defaults to the safe choice if the dialog
    // never ran.
    public EventEditScope EditScope { get; set; }
}
