using System.Text;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// Exact-byte assertion style, matching MarkdownServiceTests: plain string and regex assertions,
// no mocking and no snapshot framework. The writer is a pure function, so every fact here is
// deterministic given its inputs.
public class CalendarFeedWriterTests
{
    private static readonly ICalendarFeedWriter Writer = new CalendarFeedWriter();

    private static CalendarFeedEntry MakeEntry(
        DateOnly date,
        TimeOnly? startTime = null,
        string boardName = "The Last Bastion",
        string title = "Session 12",
        VoteType availability = VoteType.Yes,
        DateTime? createdAt = null,
        int sourceId = 1,
        CalendarFeedSource source = CalendarFeedSource.Event)
    {
        return new CalendarFeedEntry
        {
            Source = source,
            SourceId = sourceId,
            BoardName = boardName,
            Title = title,
            Date = date,
            StartTime = startTime,
            Availability = availability,
            CreatedAt = createdAt ?? new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    // Reconstructs a folded content line's full logical value by concatenating its first
    // physical line with every continuation line, stripping the single leading space each
    // continuation carries -- the exact unfolding rule RFC 5545 defines.
    private static string ExtractFoldedProperty(string body, string propertyPrefix)
    {
        var lines = body.Split("\r\n");
        var result = new StringBuilder();
        var found = false;

        foreach (var line in lines)
        {
            if (!found)
            {
                if (line.StartsWith(propertyPrefix, StringComparison.Ordinal))
                {
                    found = true;
                    result.Append(line);
                }

                continue;
            }

            if (line.StartsWith(' '))
            {
                result.Append(line[1..]);
            }
            else
            {
                break;
            }
        }

        return result.ToString();
    }

    // Reverse of the writer's escaper, applied in the opposite order the escaper used --
    // newline undo first, backslash undo last -- so a doubled escape backslash is never
    // mistaken for an escape sequence introduced by an earlier step.
    private static string UnescapeText(string escaped)
    {
        var result = escaped.Replace("\\n", "\n");
        result = result.Replace("\\,", ",");
        result = result.Replace("\\;", ";");
        result = result.Replace("\\\\", "\\");
        return result;
    }

    private static void AssertNoPhysicalLineExceeds75Octets(string body)
    {
        foreach (var line in body.Split("\r\n"))
        {
            Encoding.UTF8.GetByteCount(line).Should().BeLessThanOrEqualTo(75);
        }
    }

    [Fact]
    public void Write_TimedEntry_EmitsStartAndEndOneHourApart()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar");

        body.Should().Contain("DTSTART:20260920T190000");
        body.Should().Contain("DTEND:20260920T200000");
        body.Should().NotContain("DTSTART:20260920T190000Z");
        body.Should().NotContain("TZID");
    }

    [Fact]
    public void Write_TimedEntryLateStart_RollsDateForwardForEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(23, 30));

        var body = Writer.Write([entry], "My Calendar");

        body.Should().Contain("DTSTART:20260920T233000");
        body.Should().Contain("DTEND:20260921T003000");
    }

    [Fact]
    public void Write_NullStartTimeEntry_EmitsDateValuedStartAndExclusiveNextDayEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 21), startTime: null);

        var body = Writer.Write([entry], "My Calendar");

        body.Should().Contain("DTSTART;VALUE=DATE:20260921");
        body.Should().Contain("DTEND;VALUE=DATE:20260922");
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 2, 28)]
    [InlineData(2026, 12, 31)]
    public void Write_AllDayEntries_AlwaysSpanExactlyOneDay(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var entry = MakeEntry(date, startTime: null);

        var body = Writer.Write([entry], "My Calendar");

        var start = ExtractFoldedProperty(body, "DTSTART;VALUE=DATE:")["DTSTART;VALUE=DATE:".Length..];
        var end = ExtractFoldedProperty(body, "DTEND;VALUE=DATE:")["DTEND;VALUE=DATE:".Length..];
        var startDate = DateOnly.ParseExact(start, "yyyyMMdd");
        var endDate = DateOnly.ParseExact(end, "yyyyMMdd");

        endDate.DayNumber.Should().Be(startDate.DayNumber + 1);
    }

    [Fact]
    public void Write_AnyEntry_EmitsTransparent()
    {
        var timed = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));
        var allDay = MakeEntry(new DateOnly(2026, 9, 21), startTime: null, sourceId: 2);

        var body = Writer.Write([timed, allDay], "My Calendar");

        body.Split("TRANSP:TRANSPARENT").Length.Should().Be(3); // 2 occurrences => 3 segments
    }

    [Fact]
    public void Write_AnyEntry_EmitsSequenceZero()
    {
        var timed = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));
        var allDay = MakeEntry(new DateOnly(2026, 9, 21), startTime: null, sourceId: 2);

        var body = Writer.Write([timed, allDay], "My Calendar");

        body.Split("SEQUENCE:0").Length.Should().Be(3);
    }

    [Fact]
    public void Write_Entry_DtstampMatchesCreatedAt()
    {
        var createdAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), createdAt: createdAt);

        var body = Writer.Write([entry], "My Calendar");

        body.Should().Contain("DTSTAMP:20260304T050607Z");
    }

    [Fact]
    public void Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));
        var entries = new List<CalendarFeedEntry> { entry };

        var first = Writer.Write(entries, "My Calendar");
        Thread.Sleep(20); // simulate the clock moving between two polls
        var second = Writer.Write(entries, "My Calendar");

        second.Should().Be(first);
    }

    [Fact]
    public void Write_TitleWithSpecialCharacters_EscapesAndRoundTrips()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: "A, B; C\\D");

        var body = Writer.Write([entry], "My Calendar");

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        var unescaped = UnescapeText(summary);

        unescaped.Should().Be("[The Last Bastion] A, B; C\\D");
    }

    [Fact]
    public void Write_TitleWithLineBreak_EscapesToLiteralBackslashN()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: "Line one\nLine two");

        var body = Writer.Write([entry], "My Calendar");

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];

        summary.Should().Contain("Line one\\nLine two");
        summary.Should().NotContain("Line one\nLine two");
    }

    [Fact]
    public void Write_LongTitle_FoldsAt75OctetsAndUnfoldsToOriginal()
    {
        var longTitle = new string('a', 300);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: longTitle);

        var body = Writer.Write([entry], "My Calendar");

        AssertNoPhysicalLineExceeds75Octets(body);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        var unescaped = UnescapeText(summary);

        unescaped.Should().Be($"[The Last Bastion] {longTitle}");

        // Every continuation line for SUMMARY begins with exactly one space, never two.
        var lines = body.Split("\r\n");
        var summaryLineIndex = Array.FindIndex(lines, l => l.StartsWith("SUMMARY:", StringComparison.Ordinal));
        summaryLineIndex.Should().BeGreaterThanOrEqualTo(0);
        for (var i = summaryLineIndex + 1; i < lines.Length && lines[i].StartsWith(' '); i++)
        {
            lines[i].Should().NotStartWith("  ");
        }
    }

    [Fact]
    public void Write_MultiByteTitle_FoldsOnCharacterBoundaryAndRoundTrips()
    {
        // U+3042 (hiragana "a") is 3 bytes in UTF-8; 40 repeats is 120 bytes, forcing a fold
        // whose naive byte-count cut would land mid-character since 75 and 74 are not multiples
        // of 3.
        var multiByteTitle = new string('あ', 40);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: multiByteTitle);

        var body = Writer.Write([entry], "My Calendar");

        AssertNoPhysicalLineExceeds75Octets(body);
        body.Should().NotContain("�"); // no replacement character from a corrupted split

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        var unescaped = UnescapeText(summary);

        unescaped.Should().Be($"[The Last Bastion] {multiByteTitle}");
    }

    [Fact]
    public void Write_Document_NeverEmitsForbiddenProperties()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar");

        body.Should().NotContain("VALARM");
        body.Should().NotContain("VTIMEZONE");
        body.Should().NotContain("TZID");
        body.Should().NotContain("STATUS:CANCELLED");
        body.Should().NotContain("DESCRIPTION");
        body.Should().NotContain("URL");
    }

    [Fact]
    public void Write_EmptyEntryList_EmitsValidDocumentWithNoEvents()
    {
        var body = Writer.Write([], "My Calendar");

        body.Should().StartWith("BEGIN:VCALENDAR");
        body.Should().Contain("END:VCALENDAR");
        body.Should().NotContain("BEGIN:VEVENT");
    }

    [Fact]
    public void Write_Document_EveryLineEndsWithCrlfAndTerminatesWithCalendarEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar");

        body.Should().EndWith("END:VCALENDAR\r\n");
        body.Replace("\r\n", string.Empty).Should().NotContain("\n");
        body.Replace("\r\n", string.Empty).Should().NotContain("\r");
    }
}
