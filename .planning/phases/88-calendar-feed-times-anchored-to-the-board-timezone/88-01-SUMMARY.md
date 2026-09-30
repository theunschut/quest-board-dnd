---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 01
subsystem: calendar-feed
tags: [icalendar, rfc5545, vtimezone, tzid, timezoneinfo, aspnetcore]

requires:
  - phase: 84
    provides: hand-rolled CalendarFeedWriter and the subscription feed endpoint
  - phase: 85
    provides: finalized one-shot quest entries in the feed
  - phase: 86
    provides: IBoardClock.TimeZone, the resolved board zone
provides:
  - Zone-aware CalendarFeedWriter.Write taking the board zone as a required per-document parameter
  - TZID on every timed DTSTART/DTEND, an offset-probed generated VTIMEZONE block and an X-WR-TIMEZONE header, all from one derived tzid
  - CalendarSubscriptionService reading IBoardClock and handing its zone to the writer
  - The CALTZ-01 to CALTZ-10 requirement family registered in REQUIREMENTS.md and ROADMAP.md
affects: [88-02, 88-03, 88-04]

plan_head_before: 0d08164397a099d1845d02387bd739f9ac2ee481

actuals:
  tokens: 8300
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Board zone is a required per-document writer parameter, never an options string"
    - "VTIMEZONE built only from TimeZoneInfo.GetUtcOffset at moments (one-day steps plus whole-minute bisection), so bytes match on Windows and Linux"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
    - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs
    - QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md

key-decisions:
  - "Zone travels as a required TimeZoneInfo parameter on Write: one zone per document makes header, block and every timed line agree structurally"
  - "TZID keeps IANA-form and UTC ids verbatim, maps Windows ids via TryConvertWindowsIdToIanaId, falls back to the raw id"
  - "VTIMEZONE emitted only when a timed entry exists, spans earliest start to latest end padded one day each side, no TZNAME and no RRULE"
  - "X-WR-TIMEZONE sits directly after X-WR-CALNAME on every document"

patterns-established:
  - "Declaring a zone is not converting: stored wall-clock digits are written unchanged"

requirements-completed: [CALTZ-01, CALTZ-02, CALTZ-03, CALTZ-08, CALTZ-10]

coverage:
  - id: D1
    description: "A timed event and a finalized quest served from a live subscription address declare DTSTART/DTEND;TZID=Europe/Amsterdam with unchanged digits, one VTIMEZONE before the first VEVENT and one X-WR-TIMEZONE header"
    requirement: "CALTZ-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ServesSubscribedEvent_ToAnonymousCaller"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs (quest tracer and rescheduled-quest facts)"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_TimedEntry_EmitsStartAndEndOneHourApart"
        status: pass
    human_judgment: false
  - id: D2
    description: "CalendarSubscriptionService hands the writer IBoardClock.TimeZone and builds entries with the stored date and time untouched"
    requirement: "CALTZ-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs#GetFeedAsync_FinalizedQuest_ProducesAnUnshiftedEntry_EvenWithANonDefaultBoardClockInScope"
        status: pass
    human_judgment: false
  - id: D3
    description: "CALTZ-01 to CALTZ-10 registered in REQUIREMENTS.md (ten Pending traceability rows, coverage 144) and ROADMAP.md, CALFEED-10 annotated as superseded"
    requirement: "CALTZ-10"
    verification:
      - kind: other
        ref: "grep -c '^- \\[ \\] \\*\\*CALTZ-' .planning/REQUIREMENTS.md returns 10; grep -c '^| CALTZ-[0-9][0-9] | Phase 88 |$' .planning/ROADMAP.md returns 10"
        status: pass
    human_judgment: false

duration: 6min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 01: Board Zone Declared End to End Summary

**Zone-aware calendar feed: timed events and quests now emit DTSTART/DTEND with TZID=Europe/Amsterdam, a generated offset-probed VTIMEZONE and an X-WR-TIMEZONE header, all derived from IBoardClock.TimeZone, with stored digits untouched.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-09-30T11:02:28Z
- **Completed:** 2026-09-30T11:08:08Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments
- `ICalendarFeedWriter.Write` takes a required `TimeZoneInfo boardZone`; the writer derives one tzid (`ResolveTzid`) used by the header, the block and every timed line, with no zoneless branch.
- `AppendTimeZone` builds the VTIMEZONE purely from `GetUtcOffset` probes (leading `19700101T000000` observance plus one fixed-date observance per offset change), so it is platform independent; it is omitted when there is no timed entry.
- `CalendarSubscriptionService` gained `IBoardClock` and passes `boardClock.TimeZone`; the fetch-window lines and quest mapping are unchanged.
- The assertions the floating contract pinned were flipped (not deleted) across the writer, guard, quest-recheck and both integration test classes; an anonymous HTTP fetch now proves the zone is declared.
- Ten CALTZ requirements registered with traceability rows, coverage 144/144, and CALFEED-10 annotated as superseded.

## Task Commits

1. **Task 1: End to end, timed event and quest declare the board zone** - `a6184140` (feat)
2. **Task 2: Mint the ten CALTZ requirements** - `58215dd6` (docs)

**Plan metadata:** recorded in the docs(88-01) completion commit that follows this summary.

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs` - Write signature carrying the board zone, XML docs updated
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - ResolveTzid, FormatTzidParameter, AppendTimeZone, AppendObservance, FormatUtcOffset, zoned timed lines, X-WR-TIMEZONE, rewritten stale comments
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` - IBoardClock injection, zone passed to writer, reworded FinalizedDate comment
- `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` - AmsterdamZone argument on every Write call, flipped assertions
- `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs` - kept compiling and green with zoned assertions and rewritten comments
- `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs` - FakeBoardClock passed to the service
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` - end-to-end zone, header and single-block proof
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` - quest tracer and rescheduled-quest zoned assertions
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md` - CALTZ family, coverage 144

## Decisions Made
Followed the plan's recorded choices 1-4 as specified (per-document zone parameter, TZID string form, VTIMEZONE span and shape, header placement). The ROADMAP Phase 88 plan list already named exactly 88-01 to 88-04, so no correction was needed.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- A shell `sed -i` rewrote a CRLF test file to LF and a Python replacement embedded real line breaks inside C# string literals, which broke the build once. Both were repaired in place (CRLF restored, literal `\r\n` escapes restored) before any commit; the committed files are CRLF and build clean.

## Verification Results
- `dotnet build`: 0 errors.
- Unit filter (CalendarFeed, CalendarSubscriptionQuestRecheck, AmbientClockSeamTests): 103 passed, 0 failed (Total 103).
- Integration filter (CalendarSubscriptionFeedTests, CalendarSubscriptionQuestFeedTests, BoardTimeZoneHealthCheckTests): 51 passed, 0 failed (Total 51).
- All Task 1 and Task 2 acceptance greps pass (no conversion APIs in the writer, no planning ids in the eight files, no `.csproj` changes, `### Phase` count unchanged at 18).

## Known Stubs
None.

## Threat Flags
None. The zone id reaches the writer only as a `TimeZoneInfo` the board clock resolved, quoted and escaped as planned (T-88-01, T-88-02, T-88-03 mitigated as designed).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
Ready for 88-02 and 88-03 (wave 2): byte-level pins for clock changes, southern-hemisphere and Windows-id zones, the SEQUENCE bump and live zone-variant hosts all build on the writer and service seam landed here. `SEQUENCE:0` is untouched by design.

## Self-Check: PASSED

All ten modified files exist and are committed; commits `a6184140` and `58215dd6` are present.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
