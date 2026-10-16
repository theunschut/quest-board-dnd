using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class LinkedQuestOnDay
{
    public LinkedQuestEntry Entry { get; set; } = null!;

    // The proposed date this placement stands for. A wall-clock value: it is shown as stored
    // and never converted between time zones.
    public DateTime When { get; set; }

    public bool IsFinalized { get; set; }

    public string TimeLabel => When.ToString("HH:mm");
}
