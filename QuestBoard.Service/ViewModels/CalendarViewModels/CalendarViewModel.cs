using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class CalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<Quest> Quests { get; set; } = new();

    // Defaults to empty on purpose. The shared calendar partial is rendered from several
    // places that build their own instance of this model and never set this collection;
    // those places therefore render no events with no flag to set and nothing to remember,
    // and any future caller inherits the same safe default.
    public List<Event> Events { get; set; } = new();

    public DateTime FirstDayOfMonth => new(Year, Month, 1);
    public DateTime LastDayOfMonth => FirstDayOfMonth.AddMonths(1).AddDays(-1);

    public string MonthName => FirstDayOfMonth.ToString("MMMM yyyy");

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    public DayOfWeek FirstDayOfWeek => FirstDayOfMonth.DayOfWeek;

    public List<CalendarDay> GetCalendarDays()
    {
        var days = new List<CalendarDay>();

        // Add empty days for the start of the month
        // Adjust for Monday-first week: Sunday becomes 6, Monday becomes 0
        var firstDayOfWeek = ((int)FirstDayOfWeek + 6) % 7;
        for (int i = 0; i < firstDayOfWeek; i++)
        {
            days.Add(new CalendarDay { IsEmpty = true });
        }

        // Add actual days of the month
        for (int day = 1; day <= DaysInMonth; day++)
        {
            var date = new DateTime(Year, Month, day);
            var questsOnDay = GetQuestsForDate(date);
            var eventsOnDay = GetEventsForDate(date);

            days.Add(new CalendarDay
            {
                Date = date,
                Day = day,
                QuestsOnDay = questsOnDay,
                EventsOnDay = eventsOnDay
            });
        }

        // Add empty days at the end to complete the week
        var lastDayOfMonth = new DateTime(Year, Month, DaysInMonth);
        var lastDayOfWeek = ((int)lastDayOfMonth.DayOfWeek + 6) % 7; // Adjust for Monday-first week
        var emptyDaysAtEnd = (6 - lastDayOfWeek) % 7;

        for (int i = 0; i < emptyDaysAtEnd; i++)
        {
            days.Add(new CalendarDay { IsEmpty = true });
        }

        return days;
    }

    private List<QuestOnDay> GetQuestsForDate(DateTime date)
    {
        var questsOnDay = new List<QuestOnDay>();

        foreach (var quest in Quests)
        {
            if (quest.IsFinalized)
            {
                // For finalized quests, only show on the finalized date
                if (quest.FinalizedDate?.Date == date.Date)
                {
                    // Find the corresponding proposed date that was chosen
                    var chosenProposedDate = quest.ProposedDates
                        .FirstOrDefault(pd => pd.Date.Date == quest.FinalizedDate.Value.Date);

                    if (chosenProposedDate != null)
                    {
                        questsOnDay.Add(new QuestOnDay
                        {
                            Quest = quest,
                            ProposedDate = chosenProposedDate,
                            IsFinalized = true
                        });
                    }
                }
            }
            else
            {
                // For non-finalized quests, show all proposed dates
                foreach (var proposedDate in quest.ProposedDates)
                {
                    if (proposedDate.Date.Date == date.Date)
                    {
                        questsOnDay.Add(new QuestOnDay
                        {
                            Quest = quest,
                            ProposedDate = proposedDate,
                            IsFinalized = false
                        });
                    }
                }
            }
        }

        return questsOnDay;
    }

    private List<EventOnDay> GetEventsForDate(DateTime date)
    {
        // This is the single place the stored date type meets the calendar grid's
        // date-and-time type. No other file may convert between them.
        var dateOnly = DateOnly.FromDateTime(date);

        return [.. Events
            .Where(e => e.Date == dateOnly)
            .OrderBy(e => e.StartTime ?? TimeOnly.MinValue)
            .Select(e => new EventOnDay { Event = e })];
    }
}