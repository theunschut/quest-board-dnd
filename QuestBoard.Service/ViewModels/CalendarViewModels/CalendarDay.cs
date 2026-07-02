namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class CalendarDay
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public bool IsEmpty { get; set; }
    public List<QuestOnDay> QuestsOnDay { get; set; } = [];
}
