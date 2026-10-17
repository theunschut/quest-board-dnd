using System.Globalization;
using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class LinkedEventOnDay
{
    public LinkedEventEntry Entry { get; set; } = null!;

    public bool IsAllDay => Entry.StartTime == null;

    // Worded the same way as an own event's time label, so an event with no start time reads
    // as "All day" rather than a blank slot, and every surface that shows a linked event
    // reads this one property.
    public string TimeLabel => Entry.StartTime.HasValue ? Entry.StartTime.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : "All day";
}
