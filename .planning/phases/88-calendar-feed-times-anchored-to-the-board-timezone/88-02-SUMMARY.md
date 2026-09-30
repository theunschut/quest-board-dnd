---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 02
subsystem: calendar-feed
tags: [icalendar, rfc5545, vtimezone, tzid, sequence, byte-pins, xunit]

requires:
  - phase: 88
    plan: 01
    provides: zone-aware CalendarFeedWriter.Write and the service hand-off of IBoardClock.TimeZone
provides:
  - CalendarFeedBoardZoneGuardTests, the renamed guard pinning the zoned contract and the service zone hand-off
  - Seventeen writer facts pinning the generated time-zone block, header, placement, quoting and determinism
  - SEQUENCE:1 on every timed and all-day entry, pinned by whole-VEVENT byte assertions and by the live feed
affects: [88-03, 88-04]

plan_head_before: 1c7ead1af5bca952df538b0c16c547364d9a3186

actuals:
  tokens: 12000
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Expected VTIMEZONE blocks are asserted as one CRLF-joined string so a drifting line, order or offset fails loudly"
    - "Service zone hand-off proven with an NSubstitute Arg.Do capture compared by reference (BeSameAs) against the fake clock's zone"

key-files:
  created:
    - QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs
  modified:
    - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
  deleted:
    - QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs (renamed to the file above, history kept)

key-decisions:
  - "The guard rename was committed alone before any content change so git rename detection and log --follow keep the file history"
  - "SEQUENCE is a constant 1 with no stored data; the identifier scheme (BuildUid) is untouched"

patterns-established:
  - "A guard class is named for the contract it pins; rewriting facts in place keeps the class's role and history"

requirements-completed: [CALTZ-02, CALTZ-03, CALTZ-04, CALTZ-05, CALTZ-06, CALTZ-07, CALTZ-08, CALTZ-10]

coverage:
  - id: D1
    description: "The floating-time guard class is renamed to CalendarFeedBoardZoneGuardTests with history preserved and five rewritten facts pin the zoned contract, the never-converts rule under Auckland, the service's exact zone hand-off and the degraded UTC path"
    requirement: "CALTZ-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs (5 facts)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The writer suite pins the VTIMEZONE block for both EU clock changes, gap and overlap nights, both hemispheres, a Windows-style id, UTC, a quoted custom id, plus placement, header equality, empty and all-day documents, folding and determinism"
    requirement: "CALTZ-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs (17 new zone-document facts)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every entry carries SEQUENCE:1, pinned per whole VEVENT block and by the live feed on both fetches of a rescheduled quest with a byte-identical UID line"
    requirement: "CALTZ-10"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_AnyEntry_EmitsSequenceOne"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 02: Zone Document Byte Pins and Sequence One Summary

**The zoned calendar document is now pinned byte for byte (clock changes on both sides of the year, both hemispheres, Windows id, UTC, quoted ids), the floating-time guard is renamed and rewritten to guard the zone, and every entry moves to SEQUENCE:1.**

## Performance

- **Duration:** about 9 min
- **Tasks:** 3
- **Files modified:** 5 (one renamed)

## Accomplishments
- `CalendarFeedFloatingTimeGuardTests` became `CalendarFeedBoardZoneGuardTests` in a rename-only commit, then its five facts were rewritten: October clock-change bytes, non-default zone never converts digits, the service hands the writer the clock's own `TimeZoneInfo` instance (`BeSameAs`), a degraded clock hands over `TimeZoneInfo.Utc`, and the real writer under a degraded clock declares `TZID=UTC`.
- 17 new writer facts, each asserting an exact block or line set: March and October changes in chronological order, summer-only single observance, no `RRULE`/`TZNAME`, one block between headers and first event, header equality, empty and all-day documents, mixed document zone placement, the spring gap and autumn overlap nights, received order for same-moment entries, `W. Europe Standard Time` declared as `Europe/Berlin`, UTC with a single `+0000` observance, Auckland's September change, a reserved-character id, window measured from the latest end, 75-octet/CRLF-only folding and byte-identical re-renders.
- `SEQUENCE:1` on both writer branches; whole-VEVENT byte pins for one timed and one all-day entry; the live feed asserts `SEQUENCE:1` and no `SEQUENCE:0`, and a rescheduled quest carries exactly one `SEQUENCE:1` on both fetches beside the unchanged UID line.

## Task Commits

1. **Task 1a: Rename the guard (rename only)** - `0aacf470` (refactor)
2. **Task 1b: Rewrite the guard to pin the zone** - `7caeafdc` (test)
3. **Task 2: Writer suite for every zone-document shape** - `364198f4` (test)
4. **Task 3 RED: sequence-one facts** - `a62b9502` (test; 3 facts failed on `SEQUENCE:0`, as intended)
5. **Task 3 GREEN: SEQUENCE:1 in the writer and live-feed assertions** - `5faeab3e` (feat)

## Deviations from Plan

None - plan executed exactly as written. Task 2's 17 facts all passed against the writer as 88-01 left it, so no writer correction was needed there; the only production change is the sequence literal in Task 3.

## TDD Gate Compliance

Task 3 followed RED then GREEN (`a62b9502` failed 3 of 59 writer facts on the old value; `5faeab3e` turned them green). Tasks 1 and 2 pin behaviour 88-01 already implemented, so their facts passed on first run by design; they are characterization pins rather than new behaviour, and there was no failing-first step to record for them.

## Issues Encountered
- A shell-quoted Python edit collapsed doubled backslashes, turning the literal `\r\n` in three new C# string literals into real line breaks and producing a stray lone CR. Caught by the compiler before any commit; repaired with backslashes generated via `chr(92)`, and confirmed the file is pure CRLF afterwards. Committed files are CRLF and build clean.

## Verification Results
- Guard filter: 5 passed, 0 failed.
- Writer filter: 59 passed (was 39 plus 17 zone facts plus 3 sequence facts).
- Task 3 unit filter (CalendarFeed, CalendarSubscriptionQuestRecheck, AmbientClockSeamTests): 125 passed, 0 failed (plan floor 125).
- Task 3 integration filter (CalendarSubscriptionFeedTests, CalendarSubscriptionQuestFeedTests, BoardTimeZoneHealthCheckTests): 51 passed, 0 failed (plan floor 51).
- Acceptance greps: writer has exactly 2 non-comment `SEQUENCE:1` and 0 `SEQUENCE:0`; `BuildUid` literal unchanged; no conversion APIs or display-name reads in the writer; no planning ids in the guard, writer tests or writer; `git grep CalendarFeedFloatingTimeGuard` outside `.planning` is empty; `git log --follow` on the guard lists 5 commits including pre-phase history.

## Known Stubs
None.

## Threat Flags
None. T-88-01 to T-88-05 mitigations are each covered by a named fact (quoting/escaping, zone instance hand-off, unchanged digits, sequence pin).

## Self-Check: PASSED

The guard file, the four modified files and commits `0aacf470`, `7caeafdc`, `364198f4`, `a62b9502` and `5faeab3e` are present on the branch; no tracked changes are left uncommitted.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
