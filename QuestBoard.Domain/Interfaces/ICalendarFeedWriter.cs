using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ICalendarFeedWriter
{
    /// <summary>
    /// Renders a complete VCALENDAR document (CRLF-joined and CRLF-terminated) carrying one
    /// VEVENT per entry. Every timed entry is declared in <paramref name="boardZone"/>, and the
    /// calendar-level zone header and the time-zone block are derived from that same zone, so the
    /// three cannot disagree. Stored wall-clock digits are written unchanged: the zone says which
    /// zone the digits belong to and never moves them.
    /// </summary>
    /// <param name="entries">The entries to render.</param>
    /// <param name="calendarName">The display name of the calendar.</param>
    /// <param name="boardZone">The zone the board clock resolved.</param>
    string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName, TimeZoneInfo boardZone);

    /// <summary>
    /// Builds the stable entry identifier for one occurrence of one source. No part of the
    /// returned value may come from configuration or from the request.
    /// </summary>
    string BuildUid(CalendarFeedSource source, int sourceId);
}
