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
        AppendCalendarHeaders(builder, calendarName);

        foreach (var entry in entries)
        {
            // A null StartTime is the whole test for an all-day entry, matching
            // EventEntity.StartTime's own documented meaning. There is no third branch and no
            // "unknown duration" case.
            if (entry.StartTime.HasValue)
            {
                AppendTimedEvent(builder, entry);
            }
            else
            {
                AppendAllDayEvent(builder, entry);
            }
        }

        builder.Append("END:VCALENDAR").Append(LineBreak);
        return builder.ToString();
    }

    // Calendar-level headers, emitted in this order and exactly once per document, before the
    // first event opener.
    private static void AppendCalendarHeaders(StringBuilder builder, string calendarName)
    {
        builder.Append("BEGIN:VCALENDAR").Append(LineBreak);
        AppendFoldedLine(builder, "VERSION:2.0");
        AppendFoldedLine(builder, "PRODID:-//D&D Quest Board//Calendar Feed//EN");
        AppendFoldedLine(builder, "CALSCALE:GREGORIAN");
        AppendFoldedLine(builder, "X-WR-CALNAME:" + EscapeText(calendarName));

        // Both hints below are advisory only, never a promise: the reading application picks
        // its own polling interval (anywhere from minutes to about a day), and at least one
        // major client is reported not to rename a subscription from X-WR-CALNAME above. They
        // cost one line each and help where they are honoured, so they are emitted -- but no
        // copy anywhere in this application may promise a refresh interval or promise that the
        // reader's calendar will show this name.
        AppendFoldedLine(builder, "X-PUBLISHED-TTL:PT4H");
        AppendFoldedLine(builder, "REFRESH-INTERVAL;VALUE=DURATION:PT4H");

        // No method property: this is a plain published calendar, not a meeting invitation, and
        // adding a method would invite clients to apply invitation-update rules to a polled
        // subscription that carries no ORGANIZER/ATTENDEE at all.
    }

    // Timed branch: floating local time (no zone parameter, no trailing Z) matching the
    // codebase's own naive time model, a fixed one-hour block invented purely for rendering.
    private void AppendTimedEvent(StringBuilder builder, CalendarFeedEntry entry)
    {
        var start = entry.Date.ToDateTime(entry.StartTime!.Value);
        var end = start.AddHours(1);

        builder.Append("BEGIN:VEVENT").Append(LineBreak);
        AppendFoldedLine(builder, "UID:" + BuildUid(entry.Source, entry.SourceId));
        AppendFoldedLine(builder, "DTSTAMP:" + FormatUtcStamp(entry.CreatedAt));
        AppendFoldedLine(builder, "DTSTART:" + FormatBasicDateTime(start));
        AppendFoldedLine(builder, "DTEND:" + FormatBasicDateTime(end));
        AppendFoldedLine(builder, "SUMMARY:" + BuildSummary(entry));
        AppendFoldedLine(builder, "TRANSP:TRANSPARENT");
        AppendFoldedLine(builder, "SEQUENCE:0");
        builder.Append("END:VEVENT").Append(LineBreak);
    }

    // All-day branch: the date-valued end is exclusive per RFC 5545, so a single-day entry ends
    // on the following day -- using the same date for both, or an inclusive end, renders a
    // one-day event across two days on most clients.
    private void AppendAllDayEvent(StringBuilder builder, CalendarFeedEntry entry)
    {
        var end = entry.Date.AddDays(1);

        builder.Append("BEGIN:VEVENT").Append(LineBreak);
        AppendFoldedLine(builder, "UID:" + BuildUid(entry.Source, entry.SourceId));
        AppendFoldedLine(builder, "DTSTAMP:" + FormatUtcStamp(entry.CreatedAt));
        AppendFoldedLine(builder, "DTSTART;VALUE=DATE:" + FormatBasicDate(entry.Date));
        AppendFoldedLine(builder, "DTEND;VALUE=DATE:" + FormatBasicDate(end));
        AppendFoldedLine(builder, "SUMMARY:" + BuildSummary(entry));
        AppendFoldedLine(builder, "TRANSP:TRANSPARENT");
        AppendFoldedLine(builder, "SEQUENCE:0");
        builder.Append("END:VEVENT").Append(LineBreak);
    }

    // Composes the board-prefixed, escaped SUMMARY value shared by both branches.
    private static string BuildSummary(CalendarFeedEntry entry)
    {
        var title = "[" + entry.BoardName + "] " + entry.Title;
        return EscapeText(title);
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

    // DTSTAMP records when this representation of the entry was produced. It derives from the
    // entry's own CreatedAt rather than any ambient clock, so re-rendering the same occurrence
    // twice -- even separated by a real clock change -- produces byte-identical output.
    private static string FormatUtcStamp(DateTime value) =>
        value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    // DTSTART/DTEND for a timed entry are floating local time -- no zone designator -- matching
    // the codebase's own naive time model.
    private static string FormatBasicDateTime(DateTime value) =>
        value.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string FormatBasicDate(DateOnly value) =>
        value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    // Applied to every free-text value the writer emits (the composed title and the calendar
    // name). Board and event titles are free text a Dungeon Master types, so this is not
    // theoretical. Order matters: escaping the backslash last would double-escape the escapes
    // the earlier rules introduced.
    private static string EscapeText(string value)
    {
        var escaped = value.Replace("\\", "\\\\");
        escaped = escaped.Replace(";", "\\;");
        escaped = escaped.Replace(",", "\\,");
        escaped = escaped.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
        return escaped;
    }

    // Applied to every content line after it is fully composed and escaped. Cuts only on a
    // UTF-8 character boundary -- splitting mid-sequence would corrupt the reconstructed value
    // on the reader's side, with nothing observable on the server that produced it.
    private static void AppendFoldedLine(StringBuilder builder, string content)
    {
        builder.Append(FoldLine(content)).Append(LineBreak);
    }

    private static string FoldLine(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        if (bytes.Length <= 75)
        {
            return content;
        }

        var folded = new StringBuilder();
        var position = 0;
        var isFirstChunk = true;

        while (position < bytes.Length)
        {
            // The first physical line gets 75 octets; every continuation line is one leading
            // space plus at most 74 octets, so no physical line -- including its single-space
            // continuation marker -- exceeds 75 octets before its terminator.
            var maxChunk = isFirstChunk ? 75 : 74;
            var end = Math.Min(position + maxChunk, bytes.Length);

            // Back off while the byte at the cut point is a UTF-8 continuation byte (10xxxxxx),
            // so the chunk boundary never lands inside a multi-byte character.
            while (end > position && end < bytes.Length && (bytes[end] & 0xC0) == 0x80)
            {
                end--;
            }

            var chunkText = Encoding.UTF8.GetString(bytes, position, end - position);
            if (!isFirstChunk)
            {
                folded.Append(LineBreak).Append(' ');
            }

            folded.Append(chunkText);
            position = end;
            isFirstChunk = false;
        }

        return folded.ToString();
    }
}
