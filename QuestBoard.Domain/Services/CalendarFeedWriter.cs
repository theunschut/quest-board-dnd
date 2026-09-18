using System.Globalization;
using System.Text;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

// Hand-rolled on purpose: after dropping DESCRIPTION and URL, this feed emits a five-field
// VEVENT with no timezone and no recurrence, which is a smaller surface than adopting and
// pinning a full RFC 5545 library would justify. Stateless and pure text-in/text-out, so it
// is registered as a singleton alongside IMarkdownService.
internal class CalendarFeedWriter : ICalendarFeedWriter
{
    private const string LineBreak = "\r\n";

    /// <inheritdoc/>
    public string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName)
    {
        var builder = new StringBuilder();
        builder.Append("BEGIN:VCALENDAR").Append(LineBreak);
        builder.Append("VERSION:2.0").Append(LineBreak);
        builder.Append("PRODID:-//D&D Quest Board//Calendar Feed//EN").Append(LineBreak);
        builder.Append("CALSCALE:GREGORIAN").Append(LineBreak);

        foreach (var entry in entries)
        {
            AppendTimedEvent(builder, entry);
        }

        builder.Append("END:VCALENDAR").Append(LineBreak);
        return builder.ToString();
    }

    // Handles the timed branch only -- an entry with a null StartTime is a true all-day event
    // and is widened onto this writer by a later plan, not handled here as a placeholder.
    private void AppendTimedEvent(StringBuilder builder, CalendarFeedEntry entry)
    {
        var start = entry.Date.ToDateTime(entry.StartTime!.Value);
        var end = start.AddHours(1);

        builder.Append("BEGIN:VEVENT").Append(LineBreak);
        builder.Append("UID:").Append(BuildUid(entry.Source, entry.SourceId)).Append(LineBreak);
        builder.Append("DTSTAMP:").Append(FormatUtcStamp(entry.CreatedAt)).Append(LineBreak);
        builder.Append("DTSTART:").Append(FormatFloating(start)).Append(LineBreak);
        builder.Append("DTEND:").Append(FormatFloating(end)).Append(LineBreak);
        builder.Append("SUMMARY:[").Append(entry.BoardName).Append("] ").Append(entry.Title).Append(LineBreak);
        builder.Append("TRANSP:TRANSPARENT").Append(LineBreak);
        builder.Append("SEQUENCE:0").Append(LineBreak);
        builder.Append("END:VEVENT").Append(LineBreak);
    }

    /// <inheritdoc/>
    public string BuildUid(CalendarFeedSource source, int sourceId)
    {
        // The prefix is a fixed literal and no part of the identifier may come from
        // configuration or from the request host -- an identifier that moves when hosting
        // moves makes every subscriber's phone accumulate a duplicate of every event, with no
        // server-side remedy.
        return $"questboard-{source.ToString().ToLowerInvariant()}-{sourceId}";
    }

    // DTSTAMP records when this representation of the entry was produced, in UTC.
    private static string FormatUtcStamp(DateTime value) =>
        value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    // DTSTART/DTEND for a timed entry are floating local time -- no zone designator -- matching
    // the codebase's own naive time model.
    private static string FormatFloating(DateTime value) =>
        value.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
}
