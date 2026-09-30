using System.Text;
using System.Text.RegularExpressions;
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
    private static readonly TimeZoneInfo AmsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

    private static CalendarFeedEntry MakeEntry(
        DateOnly date,
        TimeOnly? startTime = null,
        string boardName = "The Last Bastion",
        string title = "Session 12",
        VoteType availability = VoteType.Yes,
        DateTime? createdAt = null,
        int sourceId = 1,
        CalendarFeedSource source = CalendarFeedSource.Event,
        TimeSpan? duration = null)
    {
        return new CalendarFeedEntry
        {
            Source = source,
            SourceId = sourceId,
            BoardName = boardName,
            Title = title,
            Date = date,
            StartTime = startTime,
            Duration = duration ?? TimeSpan.FromHours(1),
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

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20260920T190000\r\n");
        body.Should().Contain("DTEND;TZID=Europe/Amsterdam:20260920T200000\r\n");
        body.Should().NotContain("20260920T190000Z");
        body.Should().Contain("BEGIN:VTIMEZONE\r\n");
    }

    [Fact]
    public void Write_TimedEntryLateStart_RollsDateForwardForEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(23, 30));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20260920T233000\r\n");
        body.Should().Contain("DTEND;TZID=Europe/Amsterdam:20260921T003000\r\n");
    }

    [Fact]
    public void Write_NullStartTimeEntry_EmitsDateValuedStartAndExclusiveNextDayEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 21), startTime: null);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

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

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

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

        var body = Writer.Write([timed, allDay], "My Calendar", AmsterdamZone);

        body.Split("TRANSP:TRANSPARENT").Length.Should().Be(3); // 2 occurrences => 3 segments
    }

    [Fact]
    public void Write_AnyEntry_EmitsSequenceOne()
    {
        var timed = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));
        var allDay = MakeEntry(new DateOnly(2026, 9, 21), startTime: null, sourceId: 2);

        var body = Writer.Write([timed, allDay], "My Calendar", AmsterdamZone);

        body.Split("SEQUENCE:1\r\n").Length.Should().Be(3);
        body.Should().NotContain("SEQUENCE:0");
    }

    [Fact]
    public void Write_EmptyEntryList_EmitsNoSequenceLine()
    {
        var body = Writer.Write([], "My Calendar", AmsterdamZone);

        body.Should().NotContain("SEQUENCE:");
    }

    [Fact]
    public void Write_TimedEntry_EmitsTheExactEventBlock()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain(string.Join("\r\n",
            "BEGIN:VEVENT",
            "UID:questboard-event-1",
            "DTSTAMP:20260917T120000Z",
            "DTSTART;TZID=Europe/Amsterdam:20260920T190000",
            "DTEND;TZID=Europe/Amsterdam:20260920T200000",
            "SUMMARY:[The Last Bastion] Session 12",
            "TRANSP:TRANSPARENT",
            "SEQUENCE:1",
            "END:VEVENT") + "\r\n");
    }

    [Fact]
    public void Write_AllDayEntry_EmitsTheExactEventBlock()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 21), startTime: null, sourceId: 2);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain(string.Join("\r\n",
            "BEGIN:VEVENT",
            "UID:questboard-event-2",
            "DTSTAMP:20260917T120000Z",
            "DTSTART;VALUE=DATE:20260921",
            "DTEND;VALUE=DATE:20260922",
            "SUMMARY:[The Last Bastion] Session 12",
            "TRANSP:TRANSPARENT",
            "SEQUENCE:1",
            "END:VEVENT") + "\r\n");
    }

    [Fact]
    public void Write_Entry_DtstampMatchesCreatedAt()
    {
        var createdAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), createdAt: createdAt);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("DTSTAMP:20260304T050607Z");
    }

    [Fact]
    public void Write_EntryWithUnspecifiedKindCreatedAt_StampsTheStoredDigitsUnshifted()
    {
        // A stored UTC value comes back from the database with no kind attached. Converting it
        // would treat it as the host's local time and shift the stamp by the host's offset, so the
        // same row would stamp differently on a UTC container and on a workstation.
        var createdAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), createdAt: createdAt);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("DTSTAMP:20260304T050607Z");
    }

    [Fact]
    public void Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));
        var entries = new List<CalendarFeedEntry> { entry };

        var first = Writer.Write(entries, "My Calendar", AmsterdamZone);
        Thread.Sleep(20); // simulate the clock moving between two polls
        var second = Writer.Write(entries, "My Calendar", AmsterdamZone);

        second.Should().Be(first);
    }

    [Fact]
    public void Write_TitleWithSpecialCharacters_EscapesAndRoundTrips()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: "A, B; C\\D");

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        var unescaped = UnescapeText(summary);

        unescaped.Should().Be("[The Last Bastion] A, B; C\\D");
    }

    [Fact]
    public void Write_TitleWithLineBreak_EscapesToLiteralBackslashN()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: "Line one\nLine two");

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];

        summary.Should().Contain("Line one\\nLine two");
        summary.Should().NotContain("Line one\nLine two");
    }

    [Fact]
    public void Write_LongTitle_FoldsAt75OctetsAndUnfoldsToOriginal()
    {
        var longTitle = new string('a', 300);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), title: longTitle);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

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

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

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

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().NotContain("VALARM");
        body.Should().NotContain("STATUS:CANCELLED");
        body.Should().NotContain("DESCRIPTION");
        body.Should().NotContain("URL");
    }

    [Fact]
    public void Write_EmptyEntryList_EmitsValidDocumentWithNoEvents()
    {
        var body = Writer.Write([], "My Calendar", AmsterdamZone);

        body.Should().StartWith("BEGIN:VCALENDAR");
        body.Should().Contain("END:VCALENDAR");
        body.Should().NotContain("BEGIN:VEVENT");
    }

    [Fact]
    public void Write_Document_EveryLineEndsWithCrlfAndTerminatesWithCalendarEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().EndWith("END:VCALENDAR\r\n");
        body.Replace("\r\n", string.Empty).Should().NotContain("\n");
        body.Replace("\r\n", string.Empty).Should().NotContain("\r");
    }

    [Fact]
    public void Write_YesAnswer_EmitsPlainTitleWithNoSuffix()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), availability: VoteType.Yes);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("SUMMARY:[The Last Bastion] Session 12" + "\r\n");
    }

    [Fact]
    public void Write_MaybeAnswer_AppendsMaybeSuffix()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), availability: VoteType.Maybe);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        UnescapeText(summary).Should().Be("[The Last Bastion] Session 12 (maybe)");
    }

    [Fact]
    public void Write_NoAnswer_AppendsDeclinedSuffix()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), availability: VoteType.No);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        UnescapeText(summary).Should().Be("[The Last Bastion] Session 12 (declined)");
    }

    [Fact]
    public void Write_Summary_OpensWithBoardNameBracket()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        summary.Should().StartWith("[");
    }

    [Fact]
    public void Write_BoardNameWithComma_EscapesAndRoundTrips()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), boardName: "Smith, Jones", title: "Title");

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var summary = ExtractFoldedProperty(body, "SUMMARY:")["SUMMARY:".Length..];
        UnescapeText(summary).Should().Be("[Smith, Jones] Title");
    }

    [Fact]
    public void Write_Document_EmitsCalNameTtlAndRefreshInterval()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("X-WR-CALNAME:My Calendar");
        body.Should().Contain("X-PUBLISHED-TTL:PT4H");
        body.Should().Contain("REFRESH-INTERVAL;VALUE=DURATION:PT4H");
    }

    [Fact]
    public void Write_Document_EmitsVersionProdidCalscaleAndNoMethod()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("VERSION:2.0");
        body.Should().Contain("PRODID:");
        body.Should().Contain("CALSCALE:GREGORIAN");
        body.Should().NotContain("METHOD:");
    }

    [Fact]
    public void Write_MultipleEntries_CalendarHeadersAppearExactlyOnceBeforeFirstEvent()
    {
        var first = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 1);
        var second = MakeEntry(new DateOnly(2026, 9, 21), new TimeOnly(19, 0), sourceId: 2);

        var body = Writer.Write([first, second], "My Calendar", AmsterdamZone);

        Regex.Matches(body, "X-WR-CALNAME:").Count.Should().Be(1);
        Regex.Matches(body, "VERSION:2.0").Count.Should().Be(1);

        var firstEventIndex = body.IndexOf("BEGIN:VEVENT", StringComparison.Ordinal);
        var calNameIndex = body.IndexOf("X-WR-CALNAME:", StringComparison.Ordinal);
        calNameIndex.Should().BeLessThan(firstEventIndex);
    }

    // --- Entry-identifier invariant guard ---
    //
    // A calendar client keys its stored copy of an entry by this identifier, so if the
    // identifier ever changes shape, every existing subscriber's device accumulates a
    // duplicate of every event it already holds, with no way to withdraw the old copies from
    // the server side. The namespacing facts below defend the same door from the other side --
    // a second kind of item sharing an event's numeric id would silently overwrite it in the
    // reader's calendar. These facts must go red the moment either assumption is reintroduced.

    [Fact]
    public void BuildUid_EventFortyTwo_ReturnsExactLiteral()
    {
        Writer.BuildUid(CalendarFeedSource.Event, 42).Should().Be("questboard-event-42");
    }

    [Fact]
    public void Write_SameEntryTwice_UidLineIsIdenticalAcrossRenders()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));
        var entries = new List<CalendarFeedEntry> { entry };

        var first = Writer.Write(entries, "My Calendar", AmsterdamZone);
        Thread.Sleep(20);
        var second = Writer.Write(entries, "My Calendar", AmsterdamZone);

        var firstUid = ExtractFoldedProperty(first, "UID:");
        var secondUid = ExtractFoldedProperty(second, "UID:");

        secondUid.Should().Be(firstUid);
    }

    [Fact]
    public void BuildUid_EveryDeclaredSource_YieldsDistinctIdentifierForSameNumericId()
    {
        // Enumerated rather than listed literally: this fact turns red the instant a member is
        // added whose identifier collides with an existing one -- precisely when the mistake
        // would otherwise ship silently.
        var sources = Enum.GetValues<CalendarFeedSource>();

        var identifiers = sources.Select(s => Writer.BuildUid(s, 7)).ToHashSet();

        identifiers.Count.Should().Be(sources.Length);
    }

    [Fact]
    public void BuildUid_AnySourceAndId_MatchesAnchoredNamespacedPattern()
    {
        // An anchored regular-expression assertion on the returned string, not a search for
        // today's configured host -- a search for a particular host value would pass for any
        // other host spliced in later.
        foreach (var source in Enum.GetValues<CalendarFeedSource>())
        {
            var uid = Writer.BuildUid(source, 123);
            Regex.IsMatch(uid, @"^questboard-[a-z]+-[0-9]+$").Should().BeTrue();
        }
    }

    [Fact]
    public void Write_EmittedUidLine_EqualsBuildUidResultForSameSourceAndId()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 99, source: CalendarFeedSource.Event);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var expected = "UID:" + Writer.BuildUid(entry.Source, entry.SourceId);
        var actual = ExtractFoldedProperty(body, "UID:");

        actual.Should().Be(expected);
    }

    // --- Quest-source behaviour: duration is data, not a literal ---

    [Fact]
    public void Write_QuestSourcedEntryWithFourHourDuration_EmitsEndFourHoursAfterStart()
    {
        var start = new TimeOnly(19, 0);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), start, source: CalendarFeedSource.Quest, duration: TimeSpan.FromHours(4));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var startInstant = entry.Date.ToDateTime(start);
        var endInstant = startInstant.Add(TimeSpan.FromHours(4));

        body.Should().Contain($"DTSTART;TZID=Europe/Amsterdam:{startInstant:yyyyMMdd}T{startInstant:HHmmss}");
        body.Should().Contain($"DTEND;TZID=Europe/Amsterdam:{endInstant:yyyyMMdd}T{endInstant:HHmmss}");
    }

    [Fact]
    public void Write_QuestSourcedEntryWithTwoHourDuration_EmitsEndTwoHoursAfterStart()
    {
        var start = new TimeOnly(19, 0);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), start, source: CalendarFeedSource.Quest, duration: TimeSpan.FromHours(2));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var startInstant = entry.Date.ToDateTime(start);
        var endInstant = startInstant.Add(TimeSpan.FromHours(2));

        body.Should().Contain($"DTSTART;TZID=Europe/Amsterdam:{startInstant:yyyyMMdd}T{startInstant:HHmmss}");
        body.Should().Contain($"DTEND;TZID=Europe/Amsterdam:{endInstant:yyyyMMdd}T{endInstant:HHmmss}");
    }

    // Regression guard for the duration default: proves the event projection can keep saying
    // nothing about Duration and still get a one-hour block, exactly as before this phase.
    [Fact]
    public void Write_EventSourcedEntryBuiltWithDefaults_StillEmitsAOneHourBlock()
    {
        var start = new TimeOnly(19, 0);
        var entry = MakeEntry(new DateOnly(2026, 9, 20), start);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        var startInstant = entry.Date.ToDateTime(start);
        var endInstant = startInstant.Add(TimeSpan.FromHours(1));

        body.Should().Contain($"DTSTART;TZID=Europe/Amsterdam:{startInstant:yyyyMMdd}T{startInstant:HHmmss}");
        body.Should().Contain($"DTEND;TZID=Europe/Amsterdam:{endInstant:yyyyMMdd}T{endInstant:HHmmss}");
    }

    // --- Quest-source behaviour: the answer suffix is unreachable for a non-event source ---

    [Theory]
    [InlineData(VoteType.Yes)]
    [InlineData(VoteType.Maybe)]
    [InlineData(VoteType.No)]
    public void Write_QuestSourcedEntry_NeverAppendsAnAnswerSuffix_ForAnyAvailabilityValue(VoteType availability)
    {
        // The availability answer belongs to one source, and the enum's default value (No) is a
        // real answer rather than an absence -- an entry that never set it would otherwise
        // render a marker the reader never chose. Gating on Source before the switch is ever
        // evaluated removes that landmine for every value, not just the ones a construction
        // site remembers to avoid.
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), source: CalendarFeedSource.Quest, availability: availability);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().Contain("SUMMARY:[The Last Bastion] Session 12\r\n");
    }

    // If VoteType ever grows a fourth member, this fails the suite rather than letting the
    // theory above silently leave the new value unproven.
    [Fact]
    public void Write_QuestSourcedEntry_NoMarkerTheoryCoversEveryDeclaredAvailabilityValue()
    {
        Enum.GetValues<VoteType>().Length.Should().Be(3);
    }

    // --- Quest-source behaviour: identifier namespacing at a colliding numeric id ---

    [Fact]
    public void Write_EventAndQuestSharingNumericId_EmitTwoDistinctIdentifierLines()
    {
        var eventEntry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 7, source: CalendarFeedSource.Event);
        var questEntry = MakeEntry(new DateOnly(2026, 9, 21), new TimeOnly(19, 0), sourceId: 7, source: CalendarFeedSource.Quest);

        var body = Writer.Write([eventEntry, questEntry], "My Calendar", AmsterdamZone);

        var eventUid = "UID:" + Writer.BuildUid(CalendarFeedSource.Event, 7);
        var questUid = "UID:" + Writer.BuildUid(CalendarFeedSource.Quest, 7);

        eventUid.Should().NotBe(questUid);
        body.Should().Contain(eventUid);
        body.Should().Contain(questUid);
    }

    // --- Quest-source behaviour: always timed, never all-day ---

    [Fact]
    public void Write_QuestSourcedEntryWithStartTime_NeverEmitsADateValuedStartOrEnd()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), source: CalendarFeedSource.Quest);

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        body.Should().NotContain("DTSTART;VALUE=DATE:");
        body.Should().NotContain("DTEND;VALUE=DATE:");
        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20260920T190000");
    }

    // --- Zone document: the generated time-zone block, header and per-line zone parameters ---

    // Joins content lines the way the writer terminates them: every line, the last included,
    // ends in a carriage return and line feed.
    private static string JoinLines(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    // The substring from the block opener through the line break that ends the block closer.
    private static string ExtractTimeZoneBlock(string body)
    {
        const string closer = "END:VTIMEZONE\r\n";
        var start = body.IndexOf("BEGIN:VTIMEZONE", StringComparison.Ordinal);
        var end = body.IndexOf(closer, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        end.Should().BeGreaterThan(start);
        return body[start..(end + closer.Length)];
    }

    // Two sessions either side of the 25 October 2026 clock change.
    private static List<CalendarFeedEntry> OctoberEntries() =>
    [
        MakeEntry(new DateOnly(2026, 10, 2), new TimeOnly(18, 0), sourceId: 1, duration: TimeSpan.FromHours(4)),
        MakeEntry(new DateOnly(2026, 10, 30), new TimeOnly(18, 0), sourceId: 2, duration: TimeSpan.FromHours(4)),
    ];

    [Fact]
    public void Write_SpanReachingTheMarchClockChange_ListsBothChangesInChronologicalOrder()
    {
        var first = MakeEntry(new DateOnly(2026, 10, 2), new TimeOnly(18, 0), sourceId: 1);
        var second = MakeEntry(new DateOnly(2027, 3, 31), new TimeOnly(18, 0), sourceId: 2);

        var body = Writer.Write([first, second], "My Calendar", AmsterdamZone);

        ExtractTimeZoneBlock(body).Should().Be(JoinLines(
            "BEGIN:VTIMEZONE",
            "TZID:Europe/Amsterdam",
            "BEGIN:DAYLIGHT",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0200",
            "END:DAYLIGHT",
            "BEGIN:STANDARD",
            "DTSTART:20261025T030000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0100",
            "END:STANDARD",
            "BEGIN:DAYLIGHT",
            "DTSTART:20270328T020000",
            "TZOFFSETFROM:+0100",
            "TZOFFSETTO:+0200",
            "END:DAYLIGHT",
            "END:VTIMEZONE"));
    }

    [Fact]
    public void Write_SummerOnlySpan_EmitsOnlyTheLeadingObservance()
    {
        var entry = MakeEntry(new DateOnly(2026, 7, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        ExtractTimeZoneBlock(body).Should().Be(JoinLines(
            "BEGIN:VTIMEZONE",
            "TZID:Europe/Amsterdam",
            "BEGIN:DAYLIGHT",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0200",
            "END:DAYLIGHT",
            "END:VTIMEZONE"));
        Regex.Matches(body, "BEGIN:(STANDARD|DAYLIGHT)").Count.Should().Be(1);
    }

    [Fact]
    public void Write_ZoneDocument_NeverEmitsARecurrenceRuleOrAZoneDisplayName()
    {
        var body = Writer.Write(OctoberEntries(), "My Calendar", AmsterdamZone);

        // Display names differ by platform and recurrence rules would extend a fixed-date block
        // past the span it was computed for, so neither may appear.
        body.Should().NotContain("RRULE");
        body.Should().NotContain("TZNAME");
    }

    [Fact]
    public void Write_TimedEntries_EmitExactlyOneTimeZoneBlockAfterTheHeadersAndBeforeTheFirstEvent()
    {
        var body = Writer.Write(OctoberEntries(), "My Calendar", AmsterdamZone);

        Regex.Matches(body, "BEGIN:VTIMEZONE").Count.Should().Be(1);
        Regex.Matches(body, "END:VTIMEZONE").Count.Should().Be(1);

        var refreshIndex = body.IndexOf("REFRESH-INTERVAL", StringComparison.Ordinal);
        var blockStart = body.IndexOf("BEGIN:VTIMEZONE", StringComparison.Ordinal);
        var blockEnd = body.IndexOf("END:VTIMEZONE", StringComparison.Ordinal);
        var firstEvent = body.IndexOf("BEGIN:VEVENT", StringComparison.Ordinal);

        refreshIndex.Should().BeGreaterThanOrEqualTo(0);
        refreshIndex.Should().BeLessThan(blockStart);
        blockEnd.Should().BeLessThan(firstEvent);
    }

    [Fact]
    public void Write_ZoneHeader_AppearsOnceAfterTheCalendarNameAndMatchesEveryZoneReference()
    {
        var eventEntry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 1);
        var questEntry = MakeEntry(
            new DateOnly(2026, 9, 27), new TimeOnly(19, 0), sourceId: 2, source: CalendarFeedSource.Quest);

        var body = Writer.Write([eventEntry, questEntry], "My Calendar", AmsterdamZone);

        Regex.Matches(body, "X-WR-TIMEZONE:").Count.Should().Be(1);

        var lines = body.Split("\r\n");
        var nameIndex = Array.FindIndex(lines, l => l.StartsWith("X-WR-CALNAME:", StringComparison.Ordinal));
        nameIndex.Should().BeGreaterThanOrEqualTo(0);
        lines[nameIndex + 1].Should().Be("X-WR-TIMEZONE:Europe/Amsterdam");

        var references = Regex.Matches(body, @"(?:DTSTART|DTEND);TZID=([^:]+):");
        references.Count.Should().Be(4);
        references.Select(m => m.Groups[1].Value).Should().OnlyContain(v => v == "Europe/Amsterdam");
        lines.Should().Contain("TZID:Europe/Amsterdam");
    }

    [Fact]
    public void Write_EmptyEntryList_DeclaresTheZoneHeaderButNoBlockAndNoZoneParameter()
    {
        var body = Writer.Write([], "My Calendar", AmsterdamZone);

        body.Should().Contain("X-WR-TIMEZONE:Europe/Amsterdam\r\n");
        body.Should().NotContain("BEGIN:VTIMEZONE");
        body.Should().NotContain("TZID");
    }

    [Fact]
    public void Write_AllDayOnlyDocument_EmitsNoTimeZoneBlockAndNoZoneParameter()
    {
        var first = MakeEntry(new DateOnly(2026, 9, 21), startTime: null, sourceId: 1);
        var second = MakeEntry(new DateOnly(2026, 9, 22), startTime: null, sourceId: 2);

        var body = Writer.Write([first, second], "My Calendar", AmsterdamZone);

        body.Should().NotContain("BEGIN:VTIMEZONE");
        body.Should().NotContain("TZID");
        body.Should().Contain("X-WR-TIMEZONE:Europe/Amsterdam\r\n");
        body.Should().Contain("DTSTART;VALUE=DATE:");
    }

    [Fact]
    public void Write_MixedDocument_PutsTheZoneOnTimedLinesOnly()
    {
        var timed = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 1);
        var allDay = MakeEntry(new DateOnly(2026, 9, 20), startTime: null, sourceId: 2);

        var body = Writer.Write([timed, allDay], "My Calendar", AmsterdamZone);

        var lines = body.Split("\r\n");
        lines.Where(l => l.StartsWith("DTSTART;", StringComparison.Ordinal)).Should().Equal(
            "DTSTART;TZID=Europe/Amsterdam:20260920T190000",
            "DTSTART;VALUE=DATE:20260920");

        // The stamp is a real instant, so it stays in UTC and never carries a zone parameter.
        var stamps = lines.Where(l => l.StartsWith("DTSTAMP", StringComparison.Ordinal)).ToList();
        stamps.Count.Should().Be(2);
        stamps.Should().OnlyContain(l => Regex.IsMatch(l, @"^DTSTAMP:\d{8}T\d{6}Z$"));
    }

    [Fact]
    public void Write_EntriesInsideTheSpringGapAndTheAutumnOverlap_KeepTheirStoredDigits()
    {
        // 02:30 on 28 March 2027 does not exist in Amsterdam and 02:30 on 31 October 2027 exists
        // twice. The stored digits are written as they are and the client resolves the ambiguity.
        var gap = MakeEntry(new DateOnly(2027, 3, 28), new TimeOnly(2, 30), sourceId: 1);
        var overlap = MakeEntry(new DateOnly(2027, 10, 31), new TimeOnly(2, 30), sourceId: 2);

        var body = Writer.Write([gap, overlap], "My Calendar", AmsterdamZone);

        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20270328T023000\r\n");
        body.Should().Contain("DTSTART;TZID=Europe/Amsterdam:20271031T023000\r\n");
        ExtractTimeZoneBlock(body).Should().Be(JoinLines(
            "BEGIN:VTIMEZONE",
            "TZID:Europe/Amsterdam",
            "BEGIN:STANDARD",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+0100",
            "TZOFFSETTO:+0100",
            "END:STANDARD",
            "BEGIN:DAYLIGHT",
            "DTSTART:20270328T020000",
            "TZOFFSETFROM:+0100",
            "TZOFFSETTO:+0200",
            "END:DAYLIGHT",
            "BEGIN:STANDARD",
            "DTSTART:20271031T030000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0100",
            "END:STANDARD",
            "END:VTIMEZONE"));
    }

    [Fact]
    public void Write_TwoEntriesAtTheSameMoment_KeepTheirReceivedOrderAndTheSameZone()
    {
        var first = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 1);
        var second = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 2);

        var body = Writer.Write([first, second], "My Calendar", AmsterdamZone);

        body.IndexOf("UID:questboard-event-1", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("UID:questboard-event-2", StringComparison.Ordinal));
        body.Split("DTSTART;TZID=Europe/Amsterdam:20260920T190000\r\n").Length.Should().Be(3);
    }

    [Fact]
    public void Write_WindowsStyleZoneId_DeclaresTheIanaNameEverywhere()
    {
        var windowsZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

        var body = Writer.Write(OctoberEntries(), "My Calendar", windowsZone);
        var amsterdamBody = Writer.Write(OctoberEntries(), "My Calendar", AmsterdamZone);

        body.Should().Contain("DTSTART;TZID=Europe/Berlin:20261002T180000\r\n");
        body.Should().Contain("TZID:Europe/Berlin\r\n");
        body.Should().Contain("X-WR-TIMEZONE:Europe/Berlin\r\n");
        body.Should().NotContain("W. Europe");
        ExtractTimeZoneBlock(body).Should().Be(
            ExtractTimeZoneBlock(amsterdamBody).Replace("TZID:Europe/Amsterdam", "TZID:Europe/Berlin"));
    }

    [Fact]
    public void Write_UtcZone_DeclaresUtcWithASingleZeroOffsetObservance()
    {
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", TimeZoneInfo.Utc);

        ExtractTimeZoneBlock(body).Should().Be(JoinLines(
            "BEGIN:VTIMEZONE",
            "TZID:UTC",
            "BEGIN:STANDARD",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+0000",
            "TZOFFSETTO:+0000",
            "END:STANDARD",
            "END:VTIMEZONE"));
        body.Should().Contain("DTSTART;TZID=UTC:20260920T190000\r\n");
        body.Should().Contain("X-WR-TIMEZONE:UTC\r\n");
        body.Should().NotContain("-0000");
        body.Should().NotContain("Etc/UTC");
    }

    [Fact]
    public void Write_SouthernHemisphereSpan_ListsTheSeptemberDaylightChange()
    {
        var auckland = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        var first = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0), sourceId: 1);
        var second = MakeEntry(new DateOnly(2026, 10, 20), new TimeOnly(19, 0), sourceId: 2);

        var body = Writer.Write([first, second], "My Calendar", auckland);

        ExtractTimeZoneBlock(body).Should().Be(JoinLines(
            "BEGIN:VTIMEZONE",
            "TZID:Pacific/Auckland",
            "BEGIN:STANDARD",
            "DTSTART:19700101T000000",
            "TZOFFSETFROM:+1200",
            "TZOFFSETTO:+1200",
            "END:STANDARD",
            "BEGIN:DAYLIGHT",
            "DTSTART:20260927T020000",
            "TZOFFSETFROM:+1200",
            "TZOFFSETTO:+1300",
            "END:DAYLIGHT",
            "END:VTIMEZONE"));
    }

    [Fact]
    public void Write_ZoneIdCarryingReservedCharacters_QuotesTheParameterAndEscapesTheText()
    {
        var oddZone = TimeZoneInfo.CreateCustomTimeZone("Odd;Zone:Id,1", TimeSpan.FromHours(1), "Odd", "Odd");
        var entry = MakeEntry(new DateOnly(2026, 9, 20), new TimeOnly(19, 0));

        var body = Writer.Write([entry], "My Calendar", oddZone);

        body.Should().Contain("DTSTART;TZID=\"Odd;Zone:Id,1\":20260920T190000\r\n");
        body.Should().Contain("TZID:Odd\\;Zone:Id\\,1\r\n");
        body.Should().Contain("X-WR-TIMEZONE:Odd\\;Zone:Id\\,1\r\n");
        body.Should().Contain("TZOFFSETTO:+0100\r\n");
    }

    [Fact]
    public void Write_BlockWindow_ReachesADayPastTheLatestEnd()
    {
        // The window is measured from the latest end, so a session that runs late still has the
        // change after it described: this one starts on 23 October and ends early on the 24th, and
        // the change falls in the small hours of the 25th.
        var entry = MakeEntry(
            new DateOnly(2026, 10, 23), new TimeOnly(22, 0), duration: TimeSpan.FromHours(4));

        var body = Writer.Write([entry], "My Calendar", AmsterdamZone);

        ExtractTimeZoneBlock(body).Should().Contain(JoinLines(
            "BEGIN:STANDARD",
            "DTSTART:20261025T030000",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0100",
            "END:STANDARD"));
    }

    [Fact]
    public void Write_ZoneDocument_KeepsEveryPhysicalLineWithin75OctetsAndCrlfOnly()
    {
        var longTitle = new string('x', 300);
        var first = MakeEntry(new DateOnly(2026, 10, 2), new TimeOnly(18, 0), title: longTitle, sourceId: 1);
        var second = MakeEntry(new DateOnly(2026, 10, 30), new TimeOnly(18, 0), title: longTitle, sourceId: 2);

        var body = Writer.Write([first, second], "My Calendar", AmsterdamZone);

        AssertNoPhysicalLineExceeds75Octets(body);
        var withoutBreaks = body.Replace("\r\n", string.Empty);
        withoutBreaks.Should().NotContain("\r");
        withoutBreaks.Should().NotContain("\n");
    }

    [Fact]
    public void Write_SameZoneDocumentTwice_IsByteIdentical()
    {
        var first = Writer.Write(OctoberEntries(), "My Calendar", AmsterdamZone);
        var second = Writer.Write(OctoberEntries(), "My Calendar", AmsterdamZone);

        second.Should().Be(first);
    }
}
