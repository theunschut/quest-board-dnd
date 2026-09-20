---
phase: 86-viewer-local-times-and-correct-job-scheduling
plan: 06
subsystem: testing
tags: [timezone, board-clock, calendar-feed, architecture-test, regression-guard]

requires:
  - phase: 86-01
    provides: IBoardClock/BoardClock seam, Html.LocalTime, FakeBoardClock test double
  - phase: 86-02
    provides: RecurringJobOptionsFactory, board-timezone health check
  - phase: 86-03
    provides: EventSeriesService/GroupRepository/Calendar/Events/Series board-clock migration
  - phase: 86-04
    provides: QuestLog/Contacts/Platform Group Html.LocalTime conversions
  - phase: 86-05
    provides: Quest/Shop/Admin Html.LocalTime conversions
provides:
  - WallClockUnmovedTests — proves a finalized quest's game night and an unfinalized proposed
    date render byte-identical wall-clock text under two different non-default board zones, and
    that the wall-clock text never sits inside a <time class="local-time"> element while a real
    instant (CreatedAt) on the same page does
  - CalendarFeedFloatingTimeGuardTests — pins CalendarFeedWriter's exact floating-local-time
    DTSTART digits, proves a non-default TimeZoneInfo held in scope changes nothing, and
    exercises CalendarSubscriptionService.GetFeedAsync end to end with a non-default board clock
    in scope to prove the service has no seam for it to reach through
  - AmbientClockSeamTests — a closed, seven-file source-level guard against any of
    DateTime.Today/Now/UtcNow reappearing in the migrated paths, comment-stripped and with a
    documented exemption for EventsController's one real-instant CancelledAt write
  - Corrected PROJECT.md tech-debt entry recording FinalizedDate's actual resolved state instead
    of the false "correct for LXC host timezone" claim
  - 86-VALIDATION.md's Per-Task Verification Map, Wave 0 Requirements, and Manual-Only
    Verifications table filled in against the five completed plans
affects: []

actuals:
  tokens: 9565
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "WithWebHostBuilder zone-variant factory: mirrors BoardTimeZoneHealthCheckTests' own
      shape, pointing TimeZone:BoardTimeZoneId at a deliberately non-default, non-UTC zone per
      test run so a coincidental pass against the configured default cannot hide a regression"
    - "Comment-stripping source-text architecture test: strips Razor (@* *@), C-style block
      (/* */) and line (//) comments before matching literal ambient-clock call shapes, with a
      documented per-line exemption for one known-correct real-instant write rather than
      skipping the whole file"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  modified:
    - .planning/PROJECT.md
    - .planning/phases/86-viewer-local-times-and-correct-job-scheduling/86-VALIDATION.md

key-decisions:
  - "Used Quest/Details.cshtml (finalized quest's FinalizedDate + CreatedAt) and Quest/Manage.cshtml
    (an unfinalized quest's ProposedDate) as the two real rendered pages for WallClockUnmovedTests,
    rather than the Events board-availability page, because both already had DM-ownership and
    viewer-authentication patterns proven by existing tests (Manage_Get_WhenQuestOwner_...,
    CreateAuthenticatedDMClientAsync), keeping the new file free of new authorization wiring."
  - "CalendarFeedFloatingTimeGuardTests' third case builds CalendarSubscriptionService directly
    with NSubstitute mocks and a hand-rolled SilentLogger (the service is internal, so a dynamic
    proxy over ILogger<CalendarSubscriptionService> cannot be generated — mirrors BoardClockTests'
    own CapturingLogger and EventsOverviewAggregationTests' SilentLogger for the identical
    constraint), rather than going through the full WebApplicationFactory integration harness,
    since the claim under test (the service has no IBoardClock seam at all) is provable at the
    unit level."
  - "AmbientClockSeamTests exempts EventsController.cs's one DateTime.UtcNow occurrence (the
    SetCancelledAsync real-instant write, documented as an accepted non-fix in 86-03-SUMMARY.md)
    by matching the call site's own text (SetCancelledAsync) rather than excluding the whole file
    from the DateTime.UtcNow check — an unrelated new DateTime.UtcNow anywhere else in that file
    still fails the test."

patterns-established:
  - "Source-text architecture tests as a phase-closing regression class: read production files
    from disk with an upward-walk resolver (matching MobileCssTests' precedent), strip comments,
    and assert on literal call shapes rather than behavior — closes gaps no behavioral test can
    reach (a call site that exists but is never exercised by any request path)."

requirements-completed: [D-01, D-03, D-04, D-05, D-06, D-07]

coverage:
  - id: D1
    description: "A wall-clock game night (finalized quest FinalizedDate, and an unfinalized quest's ProposedDate) renders byte-identical text under two different non-default board zones, and is never wrapped in a <time class=\"local-time\"> element, while a real instant (CreatedAt) on the same page is"
    requirement: "D-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs (3 facts)"
        status: pass
    human_judgment: false
  - id: D2
    description: "CalendarFeedWriter's DTSTART emission for a finalized quest is pinned to the exact floating-local-time digits with no TZID/VTIMEZONE, immune to a TimeZoneInfo held in scope, and CalendarSubscriptionService.GetFeedAsync produces the same unshifted Date/StartTime even with a non-default board clock instance in scope"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs (3 facts)"
        status: pass
    human_judgment: false
  - id: D3
    description: "A closed, seven-file list (EventSeriesService, GroupRepository, CalendarController, EventsController, SeriesController, DailyReminderJob, Series/Details.cshtml) fails a comment-stripped source-text scan if DateTime.Today/Now/UtcNow reappears, with a documented exemption for EventsController's one real-instant write, and a pinned exemption count for EmailPreviewController's five cosmetic uses"
    requirement: "D-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs (15 facts); live-verified by temporarily reinserting DateTime.Today into EventsController.cs (test failed with a message naming the exact line) and reverting (test passed again)"
        status: pass
    human_judgment: false
  - id: D4
    description: "PROJECT.md's tech-debt entry describes the shipped reality (naive wall-clock FinalizedDate, board-clock-driven ambient reads, remaining type-system debt deferred) instead of the false LXC-host-timezone claim; 86-VALIDATION.md names real tasks/plans/waves instead of TBD placeholders and corrects the no-flash manual-verification instruction"
    requirement: "D-04"
    verification:
      - kind: other
        ref: "grep -c 'correct for LXC host timezone' PROJECT.md == 0; grep -c 'TBD | TBD | TBD' / '❌ W0' / 'carries an explicit zone label' in 86-VALIDATION.md == 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "The three unprovable-in-CI behaviours (container tzdata resolution, no-flash-of-UTC first paint, no game night moved on the pages that matter most) are confirmed by a human operator"
    human_judgment: true
    rationale: "Container tzdata resolution requires the actual built Docker image; first-paint flash timing requires a real browser with its zone changed; and visual confirmation that no rendered date/time moved requires a human comparing pages against pre-phase expectations. None of these are provable from the CI test host."
    verification: []

duration: in progress — halted at the blocking checkpoint
completed: 2026-09-20
status: halted
---

# Phase 86 Plan 06: Close the Phase — Wall-Clock and Calendar-Feed Guards, Ambient-Clock Seam, Corrected Tech Debt Summary

**Three regression guards (wall-clock-unmoved, calendar-feed-floating-time, ambient-clock-seam), a corrected PROJECT.md tech-debt entry, and a filled-in validation map — halted at the phase's blocking human-verification checkpoint (container tzdata, no-flash first paint, no moved game nights).**

## Performance

- **Duration:** Tasks 1-3 completed; Task 4 (blocking checkpoint) not yet reached a resolution
- **Tasks:** 3 of 4 completed
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments
- `WallClockUnmovedTests.cs`: three facts proving a finalized quest's game night (`Quest/Details`) and an unfinalized quest's proposed date (`Quest/Manage`) render byte-identical wall-clock text under two different non-default board zones (`America/New_York`, `Asia/Tokyo`, `Pacific/Auckland`), and that the wall-clock text never sits inside a `<time class="local-time">` element while a real instant (`CreatedAt`) on the same page does.
- `CalendarFeedFloatingTimeGuardTests.cs`: pins `CalendarFeedWriter`'s exact `DTSTART:20260920T190000` digits with no `TZID`/`VTIMEZONE`, proves a non-default `TimeZoneInfo` held in scope changes nothing (the writer only takes `DateOnly`/`TimeOnly`), and exercises `CalendarSubscriptionService.GetFeedAsync` end to end with a non-default board clock instance in scope to prove the service has no seam for it to reach through.
- `AmbientClockSeamTests.cs`: 15 facts enforcing a closed, seven-file list against any of `DateTime.Today`/`Now`/`UtcNow` reappearing, after stripping C#, block, and Razor comments — with a documented, line-matched exemption for `EventsController.cs`'s one real-instant `CancelledAt` write, positive assertions that each file still mentions `IBoardClock` (or `Model.Today` for the Razor view), and a pinned count confirming `EmailPreviewController`'s five cosmetic uses remain untouched. Live-verified: reinserting `var x = DateTime.Today;` into `EventsController.cs` failed the suite with a message naming the exact offending line; reverting restored green.
- `PROJECT.md`'s tech-debt bullet no longer claims `FinalizedDate` is "correct for LXC host timezone" — the shipped container sets no `TZ`, mounts no `/etc/localtime`, and installs no tzdata override, so its clock is UTC. The bullet now records the resolved state and defers the remaining type-system debt.
- `86-VALIDATION.md`'s Per-Task Verification Map names the real task/plan/wave for all six seeded rows, all four Wave 0 Requirements are checked off (with the timezone-resolver item annotated: no separate wrapped seam was built because passing an unresolvable id straight to the real `TimeZoneInfo.FindSystemTimeZoneById` already exercises the identical fallback path), the no-flash manual-verification instruction is corrected to the locked no-zone-label behavior, and `nyquist_compliant`/`wave_0_complete` are set to `true`.
- Full test suite verified green after every task: 561→576 unit tests (+15) and 853 integration tests (+3), 0 failures, at every task boundary.

## Task Commits

Each task was committed atomically:

1. **Task 1: Prove no game night moved and the calendar feed is untouched** - `041dc53d` (test)
2. **Task 2: Lock the promoted clock in with an invariant test** - `1c6dfc70` (test)
3. **Task 3: Correct the false tech-debt entry and fill in the validation map** - `aea74f93` (docs)

**Task 4 (blocking checkpoint) not yet started** — this plan halted here per its `autonomous: false` frontmatter and the task's `gate="blocking"` attribute. No self-approval or auto-answer was applied.

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs` - Wall-clock-unmoved proof across two board zones plus the structural `<time class="local-time">` marker check
- `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs` - Calendar feed floating-local-time contract pinned at the writer and the service boundary
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` - Source-level guard against ambient clock reads creeping back into the seven migrated paths
- `.planning/PROJECT.md` - Corrected `FinalizedDate` tech-debt entry
- `.planning/phases/86-viewer-local-times-and-correct-job-scheduling/86-VALIDATION.md` - Filled-in verification map, Wave 0 checklist, corrected manual-verification instruction, `nyquist_compliant`/`wave_0_complete` set true

## Decisions Made
- Used `Quest/Details.cshtml` (finalized quest) and `Quest/Manage.cshtml` (unfinalized proposed date, DM-owned) as the two real rendered pages for `WallClockUnmovedTests`, reusing existing authorization patterns (`CreateAuthenticatedDMClientAsync`, `IsQuestOwner`) rather than the Events board-availability page, to avoid introducing new authorization wiring into a phase-closing test file.
- Built `CalendarFeedFloatingTimeGuardTests`' service-boundary case with NSubstitute mocks and a hand-rolled `SilentLogger` (mirroring `BoardClockTests`' `CapturingLogger` and `EventsOverviewAggregationTests`' own idiom for the same internal-type constraint) rather than the full integration harness, since the claim under test — the service has no `IBoardClock` seam — is fully provable at the unit level.
- Exempted `EventsController.cs`'s one `DateTime.UtcNow` occurrence in `AmbientClockSeamTests` by matching the call site's own text (`SetCancelledAsync`) rather than skipping the whole file from that check, so an unrelated regression elsewhere in the file still fails.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan's literal exempt-token guidance would not have matched the real call site**
- **Found during:** Task 2 (writing `AmbientClockSeamTests.cs`)
- **Issue:** A first-draft exemption keyed on the substring `"CancelledAt"` for `EventsController.cs`'s documented `DateTime.UtcNow` write does not actually appear in that line — the real call is `await eventService.SetCancelledAsync(id, DateTime.UtcNow, token);`, which contains `"CancelledAsync"`, not `"CancelledAt"` as a contiguous substring.
- **Fix:** Changed the exempt token to `"SetCancelledAsync"`, which is literally present on that line.
- **Files modified:** QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
- **Verification:** `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~AmbientClockSeam` passes 15/15; live reinsert-then-revert check confirmed the guard still catches a genuine regression on the same file.
- **Committed in:** 1c6dfc70 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (bug in the test's own exemption match, caught before commit)
**Impact on plan:** No scope change; the fix corrects the test's own matching logic to actually implement the plan's stated intent (exempt the one documented real-instant write, not the whole file).

## Issues Encountered

None beyond the deviation documented above.

## User Setup Required

None — no external service configuration required for Tasks 1-3. Task 4 (the blocking checkpoint) requires a human to build and run the Docker image, and to check a real browser's rendered pages, per its own `<how-to-verify>` instructions.

## Next Phase Readiness

Not applicable — this is the phase's own closing plan. Tasks 1-3 close three of this phase's four named risks (shifting the calendar feed, converting a wall-clock date, fixing one clock but not the other) with guards that outlive the phase. Task 4 is the phase's last gate: the operator must confirm the container resolves its configured zone, that no flash of a wrong-looking time occurs for a viewer abroad, that no game night moved on the pages that matter most, and that the QuestLog finalized-versus-closed format asymmetry (from 86-04) is acceptable. The phase cannot be marked complete until Task 4 resolves.

## Self-Check: PASSED

All 5 files listed in Files Created/Modified verified present on disk. All 3 commit hashes (`041dc53d`, `1c6dfc70`, `aea74f93`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20 (halted at Task 4 — blocking human-verification checkpoint)*
