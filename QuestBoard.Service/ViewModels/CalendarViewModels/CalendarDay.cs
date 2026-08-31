namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class CalendarDay
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public bool IsEmpty { get; set; }
    public List<QuestOnDay> QuestsOnDay { get; set; } = [];

    // Defaults to empty so any caller of the shared calendar partial that never populates
    // events renders nothing here at all.
    public List<EventOnDay> EventsOnDay { get; set; } = [];
}
