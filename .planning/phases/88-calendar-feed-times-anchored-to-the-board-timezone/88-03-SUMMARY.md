---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 03
subsystem: calendar-feed
tags: [icalendar, tzid, vtimezone, integration-tests, architecture-guard, board-clock]

requires:
  - phase: 88
    provides: "Plan 01 zone-aware CalendarFeedWriter and CalendarSubscriptionService reading IBoardClock.TimeZone"
provides:
  - "Live anonymous-HTTP proof that the feed declares the resolved board zone under default, non-default, unresolvable and Windows-style configured ids"
  - "Source-level guard that the feed takes its zone only from the board clock"
affects: [88-04]

plan_head_before: 1c7ead1af5bca952df538b0c16c547364d9a3186

actuals:
  tokens: 6200
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Zone-variant host via WithWebHostBuilder over the fixture's shared InMemory store; mint and fetch through the variant host so its own BoardClock singleton is exercised"
    - "Closed-list architecture guard extended only by insertion, never by removal or rewording"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs
  modified:
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs

key-decisions:
  - "Seeding shared by the four facts through one SeedAndFetchAsync helper instead of four copies of the arrange block"
  - "No observance onset dates asserted over HTTP; seeded dates move with the calendar, so byte pins stay in the unit suite. UTC observance lines are asserted since UTC never changes"

patterns-established:
  - "The feed's declared zone must equal what the clock resolved: an unresolvable configured id yields UTC and never appears in the body"

requirements-completed: [CALTZ-03, CALTZ-04, CALTZ-05, CALTZ-07, CALTZ-08]

coverage:
  - id: D1
    description: "Default host: timed event and finalized quest declare TZID=Europe/Amsterdam with seeded 190000 digits, the all-day event carries VALUE=DATE with no zone, one VTIMEZONE, X-WR-TIMEZONE header"
    requirement: "CALTZ-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs#Feed_DefaultBoardZone_DeclaresEuropeAmsterdamOnTimedEntriesOnly"
        status: pass
    human_judgment: false
  - id: D2
    description: "Pacific/Auckland host declares that zone with unchanged digits and no Europe/Amsterdam text"
    requirement: "CALTZ-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs#Feed_NonDefaultBoardZone_DeclaresThatZoneWithTheStoredDigitsUnchanged"
        status: pass
    human_judgment: false
  - id: D3
    description: "Unresolvable configured id makes the feed declare UTC (one STANDARD observance, +0000 to +0000, no DAYLIGHT) and never echo the configured id"
    requirement: "CALTZ-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs#Feed_UnresolvableBoardZone_DeclaresUtcAndNeverTheConfiguredId"
        status: pass
    human_judgment: false
  - id: D4
    description: "Windows-style id W. Europe Standard Time is declared as Europe/Berlin and the Windows spelling never appears"
    requirement: "CALTZ-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs#Feed_WindowsStyleBoardZone_DeclaresAnIanaName"
        status: pass
    human_judgment: false
  - id: D5
    description: "AmbientClockSeamTests guards CalendarFeedWriter.cs and CalendarSubscriptionService.cs, lists the service as a board-clock consumer, and forbids the configured zone string in either file and a clock in the writer; no pre-existing entry changed"
    requirement: "CALTZ-08"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs#CalendarFeedSources_TakeTheZoneOnlyFromTheBoardClock"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 03: Live Board-Zone Proof and Seam Guard Summary

**Four anonymous-HTTP facts prove the feed declares exactly the zone the board clock resolved (Europe/Amsterdam, Pacific/Auckland, UTC for an unresolvable id without echoing it, Europe/Berlin for a Windows id), and the ambient-clock seam guard now covers both feed files and forbids reading the configured zone string.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-09-30T11:08:00Z
- **Completed:** 2026-09-30T11:17:00Z
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- New `CalendarFeedBoardZoneHttpTests` class with four facts, each seeding a timed event, an all-day event and a seated finalized quest, minting through the host under test and fetching anonymously.
- The unresolvable-zone fact asserts UTC in every position (`DTSTART;TZID=UTC:`, `TZID:UTC`, `X-WR-TIMEZONE:UTC`), exactly one `BEGIN:STANDARD`, no `BEGIN:DAYLIGHT`, `TZOFFSETFROM:+0000`/`TZOFFSETTO:+0000`, and that `Definitely/NotAZone` is absent from the body.
- `AmbientClockSeamTests` gained two guarded paths, one board-clock consumer path and the fact `CalendarFeedSources_TakeTheZoneOnlyFromTheBoardClock`; the diff is 39 insertions and 0 deletions.

## Task Commits

1. **Task 1: Live feed under four configured zones** - `ef3fb025` (test)
2. **Task 2: Seam guard covers the feed** - `6611531f` (test)

**Plan metadata:** the docs(88-03) commit that follows this summary.

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs` - four live-feed facts plus zone-variant host, seeding, mint and fetch helpers
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` - two guarded paths, one consumer path, one new source-level fact

## Decisions Made
- Shared the arrange block through one `SeedAndFetchAsync` helper (returns body plus the seeded days) rather than repeating it four times; the plan's named helpers are all present.
- Did not assert observance onset dates over HTTP, as the plan directed; only the UTC observance lines are asserted because UTC has no change on any date.

## Deviations from Plan

### TDD gate note

Both tasks are marked `tdd="true"` but the behavior under test was implemented by plan 88-01, so there was no failing RED state to reach. The new tests passed on first run (unexpected green, expected here because the plan is a proof and guard layer over existing behavior). The plan is `type: execute`, so no TDD gate compliance section is required. The new source fact was not separately mutation-checked.

### Acceptance-count discrepancy (cosmetic)

The Task 2 grep for `"QuestBoard.Domain/Services/CalendarSubscriptionService.cs",` returns 3 rather than the planned 2, and the writer grep returns 2 rather than 1. The extra hit in each is the local `feedPaths` array inside the new fact, which lists the same two paths with a trailing comma. No existing line changed (39 insertions, 0 deletions), which is the intent of the criterion.

**Total deviations:** 0 auto-fixed. **Impact on plan:** none.

## Issues Encountered
None.

## Verification Results
- `dotnet test QuestBoard.IntegrationTests --filter CalendarFeedBoardZoneHttpTests`: 4 passed, 0 failed.
- Calendar integration filter (CalendarSubscriptionFeedTests, CalendarSubscriptionQuestFeedTests, BoardTimeZoneHealthCheckTests): 51 passed, Total 51. Combined with the 4 new facts this reaches the plan's Total 55.
- `dotnet test QuestBoard.UnitTests --filter AmbientClockSeamTests`: 45 passed, Total 45.
- Unit filter (CalendarFeed, CalendarSubscriptionQuestRecheck, AmbientClockSeamTests): 107 passed, 0 failed.
- Planning-id grep over both files returns 0. No 88-02 files touched and no SEQUENCE assertions made.

## Known Stubs
None.

## Threat Flags
None. T-88-02, T-88-03 and T-88-06 are mitigated by the unresolvable-zone, digits-unchanged and source-guard facts as planned.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
Ready for 88-04. The merged result should be re-run once 88-02 lands (its SEQUENCE change and guard-class rename do not overlap these files).

## Self-Check: PASSED

Both files exist and are committed; commits `ef3fb025` and `6611531f` are present; commit count measured as 2 from the recorded base.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
