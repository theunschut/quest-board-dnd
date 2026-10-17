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

    // Empty by default for the same reason as Events above: the callers of the shared calendar
    // partial that build their own model and never populate these render no linked entries and
    // no linked note, with nothing to remember. They carry the read-only entries and the names
    // of the other boards in the active board's linked set, and are never merged into the
    // board's own collections.
    public List<LinkedQuestEntry> LinkedQuests { get; set; } = [];
    public List<LinkedEventEntry> LinkedEvents { get; set; } = [];
    public List<LinkedBoard> LinkedBoards { get; set; } = [];

    // Both default empty/false on purpose, for the same reason as Events above: the shared
    // calendar partial is rendered from several places that build their own instance of this
    // model and never set these, so those places carry no runway state and no manager flag
    // with nothing to remember and no flag to switch off, and any future caller inherits the
    // same safe default.
    public List<SeriesRunwayStatus> SeriesBelowRunway { get; set; } = [];
    public bool CanManage { get; set; }

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
                EventsOnDay = eventsOnDay,
                Linked = new LinkedDayEntries
                {
                    Events = GetLinkedEventsForDate(date),
                    Quests = GetLinkedQuestsForDate(date)
                }
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
        var dateOnly = ToStoredDate(date);

        return [.. Events
            .Where(e => e.Date == dateOnly)
            .OrderBy(e => e.StartTime ?? TimeOnly.MinValue)
            .Select(e => new EventOnDay { Event = e })];
    }

    // Follows the same placement rules as the board's own quests, so a linked quest never shows
    // on a day an own quest of the same shape would not: a finalized quest appears only on its
    // finalized day and only when a proposed date exists that day, and an open quest appears
    // once per proposed date on the day.
    private List<LinkedQuestOnDay> GetLinkedQuestsForDate(DateTime date)
    {
        var questsOnDay = new List<LinkedQuestOnDay>();

        foreach (var quest in LinkedQuests)
        {
            if (quest.IsFinalized)
            {
                if (quest.FinalizedDate?.Date == date.Date)
                {
                    var chosen = quest.ProposedDates.Where(pd => pd.Date == date.Date).ToList();

                    if (chosen.Count > 0)
                    {
                        questsOnDay.Add(new LinkedQuestOnDay { Entry = quest, When = chosen[0], IsFinalized = true });
                    }
                }
            }
            else
            {
                foreach (var proposed in quest.ProposedDates.Where(pd => pd.Date == date.Date))
                {
                    questsOnDay.Add(new LinkedQuestOnDay { Entry = quest, When = proposed, IsFinalized = false });
                }
            }
        }

        return [.. questsOnDay
            .OrderBy(q => q.When.TimeOfDay)
            .ThenBy(q => q.Entry.Title, StringComparer.OrdinalIgnoreCase)];
    }

    // Linked events sit in their own list so they can never reach the markup that belongs to
    // the board's own events. All-day events sort first because a missing start time orders as
    // midnight, then ties fall back to the title.
    private List<LinkedEventOnDay> GetLinkedEventsForDate(DateTime date)
    {
        var dateOnly = ToStoredDate(date);

        return [.. LinkedEvents
            .Where(e => e.Date == dateOnly)
            .OrderBy(e => e.StartTime ?? TimeOnly.MinValue)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new LinkedEventOnDay { Entry = e })];
    }

    // This is the single place where the grid's date-and-time cells meet the stored date type.
    // Placing an entry on a day must go through it; the linked read's month window, which is
    // built from midnight-aligned month starts, is converted once in the service that reads it.
    private static DateOnly ToStoredDate(DateTime date) => DateOnly.FromDateTime(date);
}