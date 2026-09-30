using System.Globalization;
using System.Text;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

// Hand-rolled on purpose: after dropping DESCRIPTION and URL, this feed emits a five-field
// VEVENT whose timed entries declare the board's zone through a generated time-zone block,
// with no recurrence rule, which is a smaller surface than adopting and pinning a full
// RFC 5545 library would justify. Stateless and pure text-in/text-out, so it is registered as
// a singleton alongside IMarkdownService.
internal class CalendarFeedWriter : ICalendarFeedWriter
{
    private const string LineBreak = "\r\n";

    /// <inheritdoc/>
    public string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName, TimeZoneInfo boardZone)
    {
        ArgumentNullException.ThrowIfNull(boardZone);

        // Derived exactly once: the calendar header, the time-zone block and every timed line
        // all use this one string, so they cannot disagree. There is no zoneless branch -- a
        // clock that fell back to UTC declares UTC through this same path.
        var tzid = ResolveTzid(boardZone);
        var tzidParameter = FormatTzidParameter(tzid);

        var builder = new StringBuilder();
        AppendCalendarHeaders(builder, calendarName, tzid);
        AppendTimeZone(builder, boardZone, tzid, entries);

        foreach (var entry in entries)
        {
            // A null StartTime is the whole test for an all-day entry, matching
            // EventEntity.StartTime's own documented meaning. There is no third branch and no
            // "unknown duration" case.
            if (entry.StartTime.HasValue)
            {
                AppendTimedEvent(builder, entry, tzidParameter);
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
    private static void AppendCalendarHeaders(StringBuilder builder, string calendarName, string tzid)
    {
        builder.Append("BEGIN:VCALENDAR").Append(LineBreak);
        AppendFoldedLine(builder, "VERSION:2.0");
        AppendFoldedLine(builder, "PRODID:-//D&D Quest Board//Calendar Feed//EN");
        AppendFoldedLine(builder, "CALSCALE:GREGORIAN");
        AppendFoldedLine(builder, "X-WR-CALNAME:" + EscapeText(calendarName));

        // A hint for clients that honour it. It names the same id as every entry's zone and the
        // time-zone block, so the three cannot disagree.
        AppendFoldedLine(builder, "X-WR-TIMEZONE:" + EscapeText(tzid));

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

    // Calendar clients such as Google resolve a zone by its IANA name, so a Windows-style id is
    // mapped to its region's canonical IANA zone. Their offsets are identical, and the
    // time-zone block below carries the rules anyway. An id already in IANA form (or the UTC
    // fallback, written as the literal id the clock resolved) is kept verbatim; with no mapping
    // available the raw id is declared rather than guessed at.
    private static string ResolveTzid(TimeZoneInfo zone)
    {
        var id = zone.Id;
        if (id.Contains('/') || id == "UTC")
        {
            return id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana) ? iana : id;
    }

    // A parameter value carrying a semicolon, colon or comma must be quoted. Ids the operating
    // system resolves never do, so this is defence in depth.
    private static string FormatTzidParameter(string tzid) =>
        tzid.IndexOfAny([';', ':', ',']) >= 0 ? "\"" + tzid + "\"" : tzid;

    // Emits one time-zone block covering the span of the timed entries, and nothing at all when
    // there are none. Offsets are found by asking the zone for its offset at a moment, because
    // Windows and Linux describe a zone's rules in different shapes (a short repeating rule
    // versus a year-by-year list) yet agree on the offset at any moment -- so this block is
    // byte-identical on both, which is why the platform's rule objects are not read. No zone
    // name is written because display names differ by platform. Observances are fixed-date and
    // carry no recurrence rule because they cover exactly the window the entries span.
    private static void AppendTimeZone(
        StringBuilder builder, TimeZoneInfo zone, string tzid, IReadOnlyList<CalendarFeedEntry> entries)
    {
        var timed = entries
            .Where(e => e.StartTime.HasValue)
            .Select(e => (Start: e.Date.ToDateTime(e.StartTime!.Value), e.Duration))
            .ToList();
        if (timed.Count == 0)
        {
            return;
        }

        // The stored digits are read as UTC instants only to bound the probe. The one-day pad
        // exists because a wall-clock value lies within a day of the same digits read as UTC in
        // every real zone, so the padded span always contains the true instants.
        var windowStart = DateTime.SpecifyKind(timed.Min(t => t.Start), DateTimeKind.Utc).AddDays(-1);
        var windowEnd = DateTime.SpecifyKind(timed.Max(t => t.Start + t.Duration), DateTimeKind.Utc).AddDays(1);

        AppendFoldedLine(builder, "BEGIN:VTIMEZONE");
        AppendFoldedLine(builder, "TZID:" + EscapeText(tzid));

        // Leading observance: the offset in effect at the start of the window, dated far in the
        // past so it precedes every entry. From equals to because nothing changes at that onset.
        var previous = zone.GetUtcOffset(windowStart);
        AppendObservance(builder, zone.IsDaylightSavingTime(windowStart), "19700101T000000", previous, previous);

        var step = TimeSpan.FromDays(1);
        for (var current = windowStart; current < windowEnd;)
        {
            var next = current + step < windowEnd ? current + step : windowEnd;
            if (zone.GetUtcOffset(next) != previous)
            {
                // Bisect on whole minutes so onsets never carry fractional seconds. The low bound
                // is still on the old offset and the high bound is already on the new one.
                long low = 0;
                long high = (long)(next - current).TotalMinutes;
                while (high - low > 1)
                {
                    var middle = (low + high) / 2;
                    if (zone.GetUtcOffset(current.AddMinutes(middle)) == previous)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                // The onset is written in the local time of the offset that applied just before
                // it, which is how a time-zone observance combines its start with its from-offset.
                var instant = current.AddMinutes(high);
                var after = zone.GetUtcOffset(instant);
                AppendObservance(
                    builder, zone.IsDaylightSavingTime(instant), FormatBasicDateTime(instant + previous), previous, after);
                previous = after;
            }

            current = next;
        }

        AppendFoldedLine(builder, "END:VTIMEZONE");
    }

    private static void AppendObservance(
        StringBuilder builder, bool daylight, string dtstartLocal, TimeSpan offsetFrom, TimeSpan offsetTo)
    {
        var kind = daylight ? "DAYLIGHT" : "STANDARD";
        AppendFoldedLine(builder, "BEGIN:" + kind);
        AppendFoldedLine(builder, "DTSTART:" + dtstartLocal);
        AppendFoldedLine(builder, "TZOFFSETFROM:" + FormatUtcOffset(offsetFrom));
        AppendFoldedLine(builder, "TZOFFSETTO:" + FormatUtcOffset(offsetTo));
        AppendFoldedLine(builder, "END:" + kind);
    }

    // The sign is always present, hours and minutes are two digits, and seconds are appended
    // only when non-zero. A zero offset is written +0000, never -0000.
    private static string FormatUtcOffset(TimeSpan offset)
    {
        var magnitude = offset.Duration();
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var text = string.Create(
            CultureInfo.InvariantCulture, $"{sign}{magnitude.Hours:00}{magnitude.Minutes:00}");
        return magnitude.Seconds != 0
            ? text + magnitude.Seconds.ToString("00", CultureInfo.InvariantCulture)
            : text;
    }

    // Timed branch: the stored wall-clock digits are written exactly as they are, with no
    // trailing Z, and the zone parameter only says which zone those digits belong to --
    // declaring a zone is not converting a time. The block's length is whatever the entry
    // carries, invented purely for rendering -- an event's default one-hour block and a quest's
    // longer configured session length are both just Duration.
    private void AppendTimedEvent(StringBuilder builder, CalendarFeedEntry entry, string tzidParameter)
    {
        var start = entry.Date.ToDateTime(entry.StartTime!.Value);
        var end = start.Add(entry.Duration);

        builder.Append("BEGIN:VEVENT").Append(LineBreak);
        AppendFoldedLine(builder, "UID:" + BuildUid(entry.Source, entry.SourceId));
        AppendFoldedLine(builder, "DTSTAMP:" + FormatUtcStamp(entry.CreatedAt));
        AppendFoldedLine(builder, "DTSTART;TZID=" + tzidParameter + ":" + FormatBasicDateTime(start));
        AppendFoldedLine(builder, "DTEND;TZID=" + tzidParameter + ":" + FormatBasicDateTime(end));
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

    // Composes the board-prefixed, answer-suffixed, escaped SUMMARY value shared by both
    // branches. The board name is a prefix and the answer is a suffix, deliberately: a second
    // prefix can consume a narrow phone day view's entire visible width before the event name
    // begins, and a suffix is what truncation should sacrifice first.
    private static string BuildSummary(CalendarFeedEntry entry)
    {
        var title = "[" + entry.BoardName + "] " + entry.Title;

        // The availability answer belongs to one source only, and the source check comes
        // before the availability switch is evaluated at all -- VoteType's default value is a
        // real answer (declined), not an absence, so an entry that never set it would otherwise
        // render a marker nobody chose. Within the event side, branches on Availability alone
        // and deliberately does not consult HasAnswered. An automatically created board-wide
        // row is a Yes that nobody chose (HasAnswered == false for it), and every Yes -- chosen
        // or not -- renders with a plain title. This is an accepted cost, not an oversight:
        // there is no third marker for the never-answered case -- a no answer marker was
        // proposed and dropped, and no such literal appears here.
        var suffix = entry.Source == CalendarFeedSource.Event
            ? entry.Availability switch
            {
                VoteType.Maybe => " (maybe)",
                VoteType.No => " (declined)",
                _ => string.Empty,
            }
            : string.Empty;

        return EscapeText(title + suffix);
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

    // The local date-time form used for zoned entry times and for time-zone observance onsets.
    // It never carries a zone designator itself; the zone is declared beside it, not inside it.
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
