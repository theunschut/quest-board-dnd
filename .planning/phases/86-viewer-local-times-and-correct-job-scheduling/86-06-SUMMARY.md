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
    Verifications table filled in against the five completed plans, with measured outcomes
    recorded for all three previously-unprovable behaviours after the human-verification
    checkpoint resolved
  - Phase-closing human-verification checkpoint resolved: container tzdata (RESEARCH.md
    Assumption A1) verified true, no-flash first-paint confirmed, calendar feed confirmed
    untouched on the live application, and the QuestLog format asymmetry closed by gap-closure
    plan 86-07
affects: []

actuals:
  tokens: 9565
  tasks: 4
  commits: 5

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
  - "The container tzdata result (RESEARCH.md Assumption A1) was recorded in 86-VALIDATION.md
    rather than editing 86-RESEARCH.md's own Assumptions Log row, because the plan's task 4 names
    no RESEARCH.md file in its scope; VALIDATION.md's row now carries the verified evidence and
    points back at the still-[CITED] RESEARCH.md row as the superseded claim."
  - "The QuestLog finalized-versus-closed format asymmetry the operator was asked to accept or
    reject at this checkpoint was rejected, not accepted — the operator chose to close the gap
    rather than live with it. Gap-closure plan 86-07 was executed and merged in response, adding
    Html.WallClock and hydrateWallClockTimes() to move all four QuestLog FinalizedDate branches
    onto locale-aware wall-clock rendering with zero timezone conversion."

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
    requirement: "D-04, D-05, D-01"
    verification:
      - kind: manual
        ref: "Human-verification checkpoint (86-06 Task 4), performed jointly by the operator (logged in, created a calendar subscription) and the orchestrator (drove the running application and the mcr.microsoft.com/dotnet/aspnet:10.0 base image): container tzdata verified present against the base image; /health returned Healthy on the Linux dev host (in-container reading deferred — docker-compose.yml pulls a published image rather than building); first-paint verified against the real hydrateLocalTimes() with a spoofed America/New_York viewer and a real Calendar Subscription 'Last fetched' tracer value; the live .ics feed confirmed floating-time on all 9 events; no game night moved on quest 12039; mobile/German/Russian locale rendering produced 0px overflow; the QuestLog format asymmetry was rejected and closed by gap-closure plan 86-07."
        status: pass
    human_judgment: true
    rationale: "Container tzdata resolution requires the actual built Docker image; first-paint flash timing requires a real browser with its zone changed; and visual confirmation that no rendered date/time moved requires a human comparing pages against pre-phase expectations. None of these are provable from the CI test host."

duration: ~25min (Tasks 1-3) + human-verification checkpoint cycle
completed: 2026-09-20
status: complete
---

# Phase 86 Plan 06: Close the Phase — Wall-Clock and Calendar-Feed Guards, Ambient-Clock Seam, Corrected Tech Debt Summary

**Three regression guards (wall-clock-unmoved, calendar-feed-floating-time, ambient-clock-seam), a corrected PROJECT.md tech-debt entry, a filled-in validation map, and a resolved phase-closing human-verification checkpoint — container tzdata verified, no-flash first paint confirmed, no game night moved, and the QuestLog format asymmetry closed by gap-closure plan 86-07.**

## Performance

- **Duration:** Tasks 1-3 completed in ~25 minutes; Task 4 (blocking checkpoint) resolved after a joint operator/orchestrator verification pass
- **Tasks:** 4 of 4 completed
- **Files modified:** 6 (3 created, 3 modified — includes the checkpoint-recording update to 86-VALIDATION.md)

## Accomplishments
- `WallClockUnmovedTests.cs`: three facts proving a finalized quest's game night (`Quest/Details`) and an unfinalized quest's proposed date (`Quest/Manage`) render byte-identical wall-clock text under two different non-default board zones (`America/New_York`, `Asia/Tokyo`, `Pacific/Auckland`), and that the wall-clock text never sits inside a `<time class="local-time">` element while a real instant (`CreatedAt`) on the same page does.
- `CalendarFeedFloatingTimeGuardTests.cs`: pins `CalendarFeedWriter`'s exact `DTSTART:20260920T190000` digits with no `TZID`/`VTIMEZONE`, proves a non-default `TimeZoneInfo` held in scope changes nothing (the writer only takes `DateOnly`/`TimeOnly`), and exercises `CalendarSubscriptionService.GetFeedAsync` end to end with a non-default board clock instance in scope to prove the service has no seam for it to reach through.
- `AmbientClockSeamTests.cs`: 15 facts enforcing a closed, seven-file list against any of `DateTime.Today`/`Now`/`UtcNow` reappearing, after stripping C#, block, and Razor comments — with a documented, line-matched exemption for `EventsController.cs`'s one real-instant `CancelledAt` write, positive assertions that each file still mentions `IBoardClock` (or `Model.Today` for the Razor view), and a pinned count confirming `EmailPreviewController`'s five cosmetic uses remain untouched. Live-verified: reinserting `var x = DateTime.Today;` into `EventsController.cs` failed the suite with a message naming the exact offending line; reverting restored green.
- `PROJECT.md`'s tech-debt bullet no longer claims `FinalizedDate` is "correct for LXC host timezone" — the shipped container sets no `TZ`, mounts no `/etc/localtime`, and installs no tzdata override, so its clock is UTC. The bullet now records the resolved state and defers the remaining type-system debt.
- `86-VALIDATION.md`'s Per-Task Verification Map names the real task/plan/wave for all six seeded rows, all four Wave 0 Requirements are checked off (with the timezone-resolver item annotated: no separate wrapped seam was built because passing an unresolvable id straight to the real `TimeZoneInfo.FindSystemTimeZoneById` already exercises the identical fallback path), the no-flash manual-verification instruction is corrected to the locked no-zone-label behavior, and `nyquist_compliant`/`wave_0_complete` are set to `true`.
- **Human-verification checkpoint (Task 4) resolved.** Performed jointly by the operator (logged in, created a calendar subscription) and the orchestrator (drove the running application and the Docker base image):
  - **Container tzdata (RESEARCH.md Assumption A1) — verified true.** `docker run --rm mcr.microsoft.com/dotnet/aspnet:10.0 ls -l /usr/share/zoneinfo/Europe/Amsterdam` returned the zone file. The Dockerfile's final stage copies only published DLLs on top of `base`, so nothing removes it — no Dockerfile change needed. `/health` returned `Healthy`, measured on the Linux dev host rather than inside the built container (the honest caveat: `docker-compose.yml` pulls a published image rather than building locally, so a true in-container `/health` reading needs a published image; the base-image tzdata check is the substantive evidence for the container case).
  - **First-paint behaviour — pass.** Verified against the real shipped `hydrateLocalTimes()` with a spoofed `America/New_York` viewer, including a real Calendar Subscription "Last fetched" tracer value (`2026-09-20T16:38:47Z` rendered `Sep 20, 2026, 6:38 PM` server-side, `20 Sept 2026, 18:38` after hydration) and a day-boundary case. No `UTC`/`CEST`/`CET`/`GMT` string appeared in visible text; `UTC` appeared only in the `title` tooltip.
  - **Calendar feed — pass.** The live `.ics` feed returned floating-time `DTSTART` values (no `Z`, no `TZID`, no `VTIMEZONE`) for all 9 events.
  - **No game night moved — pass.** Quest 12039's only rendered clock time was `6:00 pm`, matching stored `18:00:00` exactly.
  - **Mobile/locale backstop — pass.** German and Russian renderings at 375px produced 0px overflow.
  - **QuestLog format asymmetry — rejected, not accepted.** The operator chose to close the gap rather than accept it; gap-closure plan **86-07** was executed and merged, adding `Html.WallClock`/`hydrateWallClockTimes()` and moving all four QuestLog `FinalizedDate` branches onto locale-aware, zero-conversion rendering.
- Full test suite verified green after every task: 561→576 unit tests (+15) and 853→860 integration tests (+7, including 86-07's additions) across the plan and its gap-closure follow-up, 0 failures, at every task boundary.

## Task Commits

Each task was committed atomically:

1. **Task 1: Prove no game night moved and the calendar feed is untouched** - `041dc53d` (test)
2. **Task 2: Lock the promoted clock in with an invariant test** - `1c6dfc70` (test)
3. **Task 3: Correct the false tech-debt entry and fill in the validation map** - `aea74f93` (docs)
4. **SUMMARY.md written in halted state (pre-checkpoint)** - `fe8a193e` (docs)
5. **Task 4: Record the human-verification checkpoint outcome** - `ae38c02e` (docs)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs` - Wall-clock-unmoved proof across two board zones plus the structural `<time class="local-time">` marker check
- `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs` - Calendar feed floating-local-time contract pinned at the writer and the service boundary
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` - Source-level guard against ambient clock reads creeping back into the seven migrated paths
- `.planning/PROJECT.md` - Corrected `FinalizedDate` tech-debt entry
- `.planning/phases/86-viewer-local-times-and-correct-job-scheduling/86-VALIDATION.md` - Filled-in verification map, Wave 0 checklist, corrected manual-verification instruction, `nyquist_compliant`/`wave_0_complete` set true, and (Task 4) measured outcomes for all three previously-unprovable behaviours plus the checkpoint resolution record

## Decisions Made
- Used `Quest/Details.cshtml` (finalized quest) and `Quest/Manage.cshtml` (unfinalized proposed date, DM-owned) as the two real rendered pages for `WallClockUnmovedTests`, reusing existing authorization patterns (`CreateAuthenticatedDMClientAsync`, `IsQuestOwner`) rather than the Events board-availability page, to avoid introducing new authorization wiring into a phase-closing test file.
- Built `CalendarFeedFloatingTimeGuardTests`' service-boundary case with NSubstitute mocks and a hand-rolled `SilentLogger` (mirroring `BoardClockTests`' `CapturingLogger` and `EventsOverviewAggregationTests`' own idiom for the same internal-type constraint) rather than the full integration harness, since the claim under test — the service has no `IBoardClock` seam — is fully provable at the unit level.
- Exempted `EventsController.cs`'s one `DateTime.UtcNow` occurrence in `AmbientClockSeamTests` by matching the call site's own text (`SetCancelledAsync`) rather than skipping the whole file from that check, so an unrelated regression elsewhere in the file still fails.
- Recorded the container tzdata result (Assumption A1) in `86-VALIDATION.md` rather than editing `86-RESEARCH.md`'s own Assumptions Log row directly, since the plan's task 4 names no RESEARCH.md file in its scope. `86-RESEARCH.md`'s A1 row remains textually `[CITED]`; `86-VALIDATION.md`'s Manual-Only Verifications table now carries the verified evidence and points back at it.
- The QuestLog finalized-versus-closed format asymmetry was rejected at the checkpoint, not accepted — the operator chose correctness over the plan's default expectation. Gap-closure plan 86-07 was scoped, executed, and merged in direct response, rather than deferring the fix to a future phase.

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

**Coverage gaps reached during human verification (limitations, not failures):**
- The closed-quest side of the QuestLog comparison could not be exercised visually: all 7 closed quests live in `GroupId=4` while the active board is `GroupId=1`, so they return 404 on multi-tenancy scoping. Pre-existing; this phase touched only the four QuestLog *view* files, not `QuestLogController.cs`.
- No `Events` or `EventSeries` rows exist in the dev dataset, so the events/series path was covered by CI only (`EventSeriesServiceTests`).

**Incidental finding, out of scope, recorded for the record:**
- `Quest/Manage` on mobile renders no signup timestamps at all — a pre-existing content gap in the `.Mobile.cshtml` twins that 86-05 correctly declined to fill. The result is a desktop/mobile asymmetry that predates this phase and is NOT a phase-86 regression.

**The QuestLog format asymmetry itself is not an issue** — it was surfaced, deliberated, and closed via gap-closure plan 86-07, documented above under Decisions Made.

## User Setup Required

None — no external service configuration was required beyond the operator's own actions already captured as part of Task 4's verification (logging in and creating a calendar subscription to produce a real tracer value).

## Next Phase Readiness

Phase 86 is complete. All four of the phase's named risks (shifting the calendar feed, converting a wall-clock date, fixing one clock but not the other, and the container's tzdata assumption) are closed by guards that outlive the phase, and the human-verification checkpoint that could not be closed by automation alone has been resolved with measured evidence recorded in `86-VALIDATION.md`. The QuestLog format asymmetry flagged during verification was closed by gap-closure plan 86-07, which is itself complete and merged. No further plans are queued in this phase.

## Self-Check: PASSED

All 6 files listed in Files Created/Modified verified present on disk. All 5 commit hashes (`041dc53d`, `1c6dfc70`, `aea74f93`, `fe8a193e`, `ae38c02e`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20*
