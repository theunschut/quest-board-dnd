using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class EventOnDay
{
    public Event Event { get; set; } = null!;

    public bool IsAllDay => Event.StartTime == null;

    // An event with no start time is worded rather than left blank, because an empty time
    // slot is indistinguishable from a rendering failure. Both the desktop chip and the
    // mobile agenda entry read this one property, so the two platforms cannot drift apart.
    public string TimeLabel => Event.StartTime.HasValue ? Event.StartTime.Value.ToString("HH:mm") : "All day";
}
