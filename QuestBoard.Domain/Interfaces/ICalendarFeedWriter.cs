using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ICalendarFeedWriter
{
    /// <summary>
    /// Renders a complete VCALENDAR document (CRLF-joined and CRLF-terminated) carrying one
    /// VEVENT per entry.
    /// </summary>
    string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName);

    /// <summary>
    /// Builds the stable entry identifier for one occurrence of one source. No part of the
    /// returned value may come from configuration or from the request.
    /// </summary>
    string BuildUid(CalendarFeedSource source, int sourceId);
}
