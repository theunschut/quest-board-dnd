---
phase: 86-viewer-local-times-and-correct-job-scheduling
plan: 03
subsystem: infra
tags: [timezone, board-clock, di, event-series, razor]

requires:
  - phase: 86-01
    provides: IBoardClock/BoardClock seam, FakeBoardClock test double
provides:
  - EventSeriesService, GroupRepository, CalendarController, EventsController and
    SeriesController all read the board's own wall-clock zone through IBoardClock instead of
    the container's UTC clock
  - SeriesDetailsViewModel.Today/TodayLabel — the controller-fills-the-view-model pattern for
    Series/Details.cshtml's own ambient read, the seventh D-07 site CONTEXT.md's file list
    missed
  - EventSeriesServiceTests.cs — a new regression-net file that did not exist before this phase
affects: [86-02, 86-04, 86-05, 86-06]

actuals:
  tokens: 8475
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "IBoardClock constructor injection extended to five more consumers (EventSeriesService,
      GroupRepository, CalendarController, EventsController, SeriesController), mirroring
      86-01's own CalendarSubscriptionService/TimeProvider precedent"
    - "Controller-fills-the-view-model for a Razor-level ambient read: SeriesController sets
      viewModel.Today from boardClock.Today; the view consumes Model.Today/Model.TodayLabel
      instead of calling DateTime.Today itself"

key-files:
  created:
    - QuestBoard.UnitTests/Services/EventSeriesServiceTests.cs
  modified:
    - QuestBoard.Domain/Services/EventSeriesService.cs
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Controllers/Events/SeriesController.cs
    - QuestBoard.Service/ViewModels/SeriesViewModels/SeriesDetailsViewModel.cs
    - QuestBoard.Service/Views/Series/Details.cshtml
    - QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs
    - QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs

key-decisions:
  - "EventSeriesServiceTests uses NSubstitute .Received(...) argument-matching (mirroring
    EventsOverviewAggregationTests' own idiom) rather than asserting on repository-level date
    filtering the mocked repository doesn't actually perform. Each test either flips the fake
    clock relative to a fixed anchor (PreviewAsync, ApplyTemplateToFutureAsync,
    CreateWithFirstPassAsync) or pins the exact DateOnly argument the service must pass to a
    mocked repository call (GetActiveSeriesForActiveGroupAsync, GetSeriesBelowRunwayAsync,
    TopUpAsync), so a regression back to the ambient clock fails the assertion regardless of
    the host's real calendar date on the day the suite runs."
  - "EventSeriesMaterializationTests (pre-existing, not in this plan's files_modified list)
    broke on the new IBoardClock constructor parameter. Fixed by giving its CreateService
    helper a FakeBoardClock fixed to the host's own DateTime.Today, so every one of that file's
    own `var today = DateOnly.FromDateTime(DateTime.Today);` local variables still lines up
    with what the service resolves internally -- zero behavior change to that file's own tests."
  - "GroupRepositoryTests' ten construction sites route through a single new CreateBoardClock()
    helper (also fixed to the host's own today by default), rather than repeating the
    FakeBoardClock literal ten times, per the plan's explicit instruction."

requirements-completed: [D-07]

coverage:
  - id: D1
    description: "EventSeriesService's seven ambient DateTime.Today reads (PreviewAsync, CreateWithFirstPassAsync, TopUpAsync, GetActiveSeriesForActiveGroupAsync, GetSeriesBelowRunwayAsync, GetRemovalImpactAsync, ApplyTemplateToFutureAsync) all resolve through IBoardClock.boardClock.Today"
    requirement: "D-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesServiceTests.cs (6 facts)"
        status: pass
    human_judgment: false
  - id: D2
    description: "GroupRepository's campaign auto-signup sweep filters against boardClock.Today instead of the container's UTC today"
    requirement: "D-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#AddMemberAsync_CampaignBoard_BackfillUsesBoardClockToday_NotHostClock"
        status: pass
    human_judgment: false
  - id: D3
    description: "CalendarController's month/year default and EventsController's Create form default date read boardClock.Now/boardClock.Today instead of DateTime.Now/DateTime.Today"
    requirement: "D-07"
    verification:
      - kind: other
        ref: "grep -vE comment-strip | grep -cE 'DateTime\\.(Today|Now|UtcNow)' on both controller files returns 0; dotnet build exits 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "SeriesController.EndAsync's cut-off date and SeriesDetailsViewModel.Today/TodayLabel (consumed by Series/Details.cshtml's Today divider and end-series confirmation) both read the board clock, closing the seventh D-07 site the original CONTEXT.md file list missed"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests --filter FullyQualifiedName~Series (20 facts)"
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-09-20
status: complete
---

# Phase 86 Plan 03: EventSeriesService, GroupRepository, and Series' Ambient Clock Reads Summary

**Seven `EventSeriesService` clock reads, the campaign auto-signup sweep, two controller date defaults, and the Series detail page's "Today" divider all moved from ambient `DateTime.Today`/`DateTime.Now` onto the shared `IBoardClock` seam, closing a seventh D-07 site (`Series/Details.cshtml`) the phase's own CONTEXT.md file list missed.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-09-20 (approx.)
- **Completed:** 2026-09-20
- **Tasks:** 3
- **Files modified:** 9 (1 new)

## Accomplishments
- `EventSeriesService` takes `IBoardClock` by constructor injection; all seven `var today = DateOnly.FromDateTime(DateTime.Today);` sites (`PreviewAsync`, `CreateWithFirstPassAsync`, `TopUpAsync`, `GetActiveSeriesForActiveGroupAsync`, `GetSeriesBelowRunwayAsync`, `GetRemovalImpactAsync`, `ApplyTemplateToFutureAsync`) now read `boardClock.Today`.
- A new `EventSeriesServiceTests.cs` — a file that did not exist before this phase — pins six clock-dependent branches against a `FakeBoardClock` set to a date the host's own clock does not share, so a regression back to the ambient read fails deterministically.
- `GroupRepository`'s campaign auto-signup sweep (the one that decides which events count as "today or later" when a member joins a campaign board) now reads `boardClock.Today` instead of the container's UTC today, closing the two-hour window after midnight where it silently skipped a board-local today.
- `CalendarController`'s month/year default (`boardClock.Now`) and `EventsController`'s Create form default date (`boardClock.Today`) both moved onto the same seam.
- `SeriesController` now injects `IBoardClock`: `Details` fills a new `SeriesDetailsViewModel.Today`/`TodayLabel` pair from `boardClock.Today`, and `EndAsync`'s series cut-off date reads `boardClock.Today` instead of `DateTime.Today`. `Series/Details.cshtml` no longer calls the ambient clock at all — it consumes `Model.Today`/`Model.TodayLabel` for the "Today" divider and the end-series confirmation label, so the view and the controller can never disagree about which day it is.

## Task Commits

Each task was committed atomically:

1. **Task 1: Move EventSeriesService's seven clock reads onto the board clock, with the regression net it never had** - `a46b554d` (test)
2. **Task 2: Move the repository sweep and the two controller defaults onto the board clock** - `6403559d` (fix)
3. **Task 3: Route Series' "today" through the view model instead of the Razor view** - `d878a649` (fix)

**Plan metadata:** _pending_ (docs: complete plan)

## Files Created/Modified
- `QuestBoard.Domain/Services/EventSeriesService.cs` - All seven ambient clock reads replaced with `boardClock.Today`; new `IBoardClock boardClock` constructor parameter
- `QuestBoard.UnitTests/Services/EventSeriesServiceTests.cs` - New regression-net file, six facts covering `PreviewAsync`, `GetActiveSeriesForActiveGroupAsync`, `GetSeriesBelowRunwayAsync`, `TopUpAsync`, `ApplyTemplateToFutureAsync`, `CreateWithFirstPassAsync`
- `QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs` - Pre-existing test file's `CreateService` helper updated for the new constructor parameter (fixed to host's own today, zero behavior change)
- `QuestBoard.Repository/GroupRepository.cs` - Campaign auto-signup sweep reads `boardClock.Today`; new `IBoardClock boardClock` constructor parameter
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - Month/year default reads `boardClock.Now`
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Create form default date reads `boardClock.Today`
- `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs` - All ten `GroupRepository` construction sites updated via new `CreateBoardClock()` helper; new test pinning the sweep against the board clock's today
- `QuestBoard.Service/Controllers/Events/SeriesController.cs` - `Details` fills `viewModel.Today`; `EndAsync` reads `boardClock.Today`; new `IBoardClock boardClock` constructor parameter
- `QuestBoard.Service/ViewModels/SeriesViewModels/SeriesDetailsViewModel.cs` - New `Today`/`TodayLabel` members, following the existing computed-property convention (`CadenceLabel`, `TimeLabel`)
- `QuestBoard.Service/Views/Series/Details.cshtml` - Both ambient reads deleted; consumes `Model.Today`/`Model.TodayLabel`

## Decisions Made
- `EventSeriesServiceTests` uses NSubstitute `.Received(...)` argument-matching and clock-flip patterns rather than testing repository-level date filtering the mocked repository doesn't perform — see key-decisions above for the full rationale.
- Fixed `EventSeriesMaterializationTests` and `GroupRepositoryTests` construction sites with a `FakeBoardClock` pinned to the host's own `DateTime.Today` by default, preserving those files' pre-existing behavior exactly while satisfying the new constructor signature.
- Left `EventsController.cs:409`'s `DateTime.UtcNow` (assigned to `SetCancelledAsync`'s `CancelledAt` parameter) untouched — see Deviations below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] EventSeriesMaterializationTests broke on the new EventSeriesService constructor parameter**
- **Found during:** Task 1 (build after adding `IBoardClock boardClock` to `EventSeriesService`)
- **Issue:** `EventSeriesMaterializationTests.cs` — a pre-existing test file not in this plan's `files_modified` list — constructs `EventSeriesService` directly and failed to compile once the constructor gained a required `IBoardClock` parameter (CS7036).
- **Fix:** Added a `FakeBoardClock` fixed to `DateOnly.FromDateTime(DateTime.Today)` in that file's `CreateService` helper, so its own local `var today = DateOnly.FromDateTime(DateTime.Today);` variables (used throughout that file's assertions) still line up with what the service now resolves internally. Zero behavior change to that file's own 15 tests.
- **Files modified:** QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs
- **Verification:** Full `dotnet test QuestBoard.UnitTests` run (554/554 passed, no regressions).
- **Committed in:** a46b554d (Task 1 commit)

### Notable Non-Fixes

**2. Acceptance criteria's `EventsController.cs` grep would flag an unrelated, correct `DateTime.UtcNow`**
- **Found during:** Task 2 verification
- **Issue:** The plan's acceptance criteria for Task 2 requires the comment-stripped `EventsController.cs` to contain zero occurrences of `DateTime.(Today|Now|UtcNow)`. Line 409 assigns `DateTime.UtcNow` to `SetCancelledAsync`'s `CancelledAt` parameter — `CancelledAt` is explicitly classified in 86-CONTEXT.md's "Real instants — stored UTC, converted for display" list, unrelated to D-07's "ambient clock read compared against a board-local date" scope. `EventsController`'s read_first section only named line 95 (the `Create()` GET default), and the plan's grep is broader than the site list it was written against.
- **Resolution:** Left `DateTime.UtcNow` at line 409 unchanged. Converting a real-instant UTC write to a board-zone read would be actively wrong — `IBoardClock` has no UTC-instant accessor, and doing so would violate D-01's own classification table. The literal grep in the plan's acceptance criteria for this one file does not hold (returns 1, not 0); every other Task 2 acceptance criterion (build, tests, `GroupRepository`/`CalendarController` greps, `boardClock.Now` count, construction-site count) passes.
- **Files modified:** none
- **Verification:** `dotnet build` exits 0; `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~GroupRepository` passes 10/10 (was 9); full `dotnet test QuestBoard.UnitTests` passes 554/554.

---

**Total deviations:** 1 auto-fixed (blocking), 1 documented non-fix (correct-as-is acceptance criteria mismatch)
**Impact on plan:** No scope growth. The auto-fix is a same-behavior test-infrastructure adjustment; the non-fix preserves an intentionally out-of-scope, already-correct real-instant write.

## Issues Encountered

None beyond the deviations documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All D-07 sites named in 86-CONTEXT.md are now closed: `EventSeriesService` (7), `GroupRepository` (1), `CalendarController` (1), `EventsController` (1), `SeriesController` (2: `Details`/`EndAsync`), plus the seventh `Series/Details.cshtml` site this plan closed via the view model. `DailyReminderJob`, the sixth CONTEXT.md-named file, remains 86-02's responsibility per the plan's own scope note.
- `EmailPreviewController`'s five `DateTime.Today` uses were not touched (confirmed cosmetic, out of scope) — grep count still 5.
- No blockers for 86-04/86-05/86-06 (real-instant render sites) or 86-06 (final phase-wide verification).

## Self-Check: PASSED

All 9 files listed in Files Created/Modified verified present on disk. All 3 task commit hashes (`a46b554d`, `6403559d`, `d878a649`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20*
