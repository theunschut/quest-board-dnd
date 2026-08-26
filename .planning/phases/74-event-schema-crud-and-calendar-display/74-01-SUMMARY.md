---
phase: 74-event-schema-crud-and-calendar-display
plan: 01
subsystem: testing
tags: [xunit, fluentassertions, integration-tests, wave-0-red]

# Dependency graph
requires: []
provides:
  - Two RED integration test files (EventsControllerIntegrationTests.cs, EventCalendarPartialTests.cs) that compile against route literals only, giving every later plan in this phase a sub-60-second automated feedback signal
  - The D-09 structural-protection acceptance test (Quest Details renders zero event markup on both desktop and mobile) as a first-class automated test before any implementation
  - The EVENT-05 quest-creation-unaffected acceptance test as a first-class automated test before any implementation
affects: [74-02, 74-03, 74-04, 74-05, 74-06, 74-07, 74-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Wave 0 RED scaffold: reference not-yet-built controller routes as string literals (not C# symbols) so the test project compiles before the controller exists"
    - "Structural-protection test verifies its own precondition succeeded (event creation returned Redirect) before asserting the negative, so the test fails loudly instead of passing vacuously while the dependency is still unimplemented"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs
  modified: []

key-decisions:
  - "Used a non-finalized quest with a matching proposed date (not a finalized quest) as the D-09 test fixture, since Quest Details only renders the shared _Calendar partial on the non-finalized branch — a finalized quest's Details page shows a static 'Quest Finalized!' summary and never calls the partial, which would make the 'no event markup' assertion pass vacuously"
  - "Added an explicit assertion that each fact's own /Events/Create POST returned a Redirect before asserting the negative outcome, so the two structural-protection facts and the quest-creation-unaffected fact fail (RED) for the right reason instead of passing for free while /Events/Create still 404s"

patterns-established:
  - "EventBlockClass / EventChipClass const string fields as the single source of truth for the calendar event chip's CSS class identity, referenced by every assertion in EventCalendarPartialTests.cs"

requirements-completed: [EVENT-01, EVENT-02, EVENT-05]

coverage:
  - id: D1
    description: "Route-based RED scaffold exercising /Events/Create, /Events/Edit/{id}, /Events/Details/{id}, /Events/Delete (10 facts covering DM-only access, valid create + month redirect, validation, past-date/no-start-time acceptance, board-member read, cross-DM edit, DM-only delete + month redirect)"
    requirement: "EVENT-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs — dotnet test --filter FullyQualifiedName~EventsControllerIntegrationTests (10/10 fail as expected RED)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Quest Details page (desktop + mobile) with a same-day event on the same board renders zero event markup"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs#QuestDetails_WithSameDayEventOnSameBoard_RendersNoEventMarkup and #QuestDetailsMobile_WithSameDayEventOnSameBoard_RendersNoEventMarkup"
        status: pass
    human_judgment: false
  - id: D3
    description: "Quest creation succeeds and persists unchanged when an event already exists on the chosen date"
    requirement: "EVENT-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs#QuestCreate_WithExistingEventOnChosenDate_SucceedsUnchanged"
        status: pass
    human_judgment: false

# Metrics
duration: 12min
completed: 2026-08-26
status: complete
---

# Phase 74 Plan 01: Wave 0 RED Test Scaffold Summary

**Two RED integration test files (13 facts total) covering Events CRUD routes, the Quest Details structural-protection guarantee, and quest-creation independence from events — all failing at 404 until later plans in this phase land the production code.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-08-26T13:33:00Z
- **Completed:** 2026-08-26T13:45:00Z
- **Tasks:** 2
- **Files modified:** 2 (both created)

## Accomplishments

- `EventsControllerIntegrationTests.cs` — 10 RED facts exercising `/Events/Create`, `/Events/Edit/{id}`, `/Events/Details/{id}`, `/Events/Delete` as route string literals, covering DM-only access, month-based redirect on create/delete, missing-title validation, past-date and missing-start-time acceptance, board-member read access, and cross-DM edit authorization
- `EventCalendarPartialTests.cs` — 3 RED facts: the D-09 structural-protection assertion (desktop and mobile) that Quest Details never leaks event markup, plus the EVENT-05 assertion that quest creation is provably unaffected by an existing same-day event
- Whole solution still compiles (`dotnet build` exits 0) with zero production `Event*` types created
- `git status --porcelain` for this plan touches only the two new test files — `Views/Quest/Details.cshtml` and `Details.Mobile.cshtml` are untouched

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the Events CRUD route-based RED scaffold** - `3657fb9` (test)
2. **Task 2: Create the structural-protection and quest-creation-unaffected tests** - `494211b` (test)

_Note: no separate plan-metadata commit in worktree mode — the orchestrator commits STATE.md/ROADMAP.md centrally after merge._

## Files Created/Modified

- `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` - 10 RED facts targeting Events CRUD routes as string literals
- `QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs` - 3 RED facts for the D-09 structural-protection guarantee and EVENT-05 quest-creation independence

## Decisions Made

- Used a non-finalized quest with a proposed date matching the event's date (rather than a finalized quest, as the plan's literal arrange steps described) as the fixture for both D-09 facts. Quest Details only calls the shared `_Calendar` partial from its non-finalized branch — a finalized quest's Details page renders a static "Quest Finalized!" summary and never reaches the partial at all. Using a finalized quest would have made the "no event markup" assertion trivially true regardless of whether the protection genuinely holds, defeating the stated purpose of the acceptance criterion (a test that catches a regression, not one that always passes).
- Added an explicit assertion in all three `EventCalendarPartialTests.cs` facts that the fact's own `/Events/Create` POST returned `Redirect`, immediately after that POST. Without it, all three facts passed even in the current RED state (since `/Events/Create` 404s today, no event is ever created, so "no event markup" and "quest creation unaffected" both trivially hold). This ensures the suite fails for the intended reason — because the feature doesn't exist yet — rather than passing for the wrong reason.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed vacuous D-09 test fixture (finalized quest never renders the calendar partial)**
- **Found during:** Task 2 (writing `QuestDetails_WithSameDayEventOnSameBoard_RendersNoEventMarkup`)
- **Issue:** The plan's literal arrange steps specified `isFinalized: true` plus setting `FinalizedDate`, mirroring `CalendarControllerIntegrationTests`'s precedent for the `/Calendar` page. But `Views/Quest/Details.cshtml`'s three `_Calendar` partial call sites (lines 604, 648, 696) and `Details.Mobile.cshtml`'s two call sites (lines 158, 196) are all gated behind `Model.Quest?.IsFinalized == false` — a finalized quest's Details page shows a "Quest Finalized!" summary instead and never calls the partial. Written as originally described, the "renders no event markup" assertion would pass trivially for every finalized quest regardless of whether the D-09 protection code exists, silently failing to test the acceptance criterion it exists to enforce.
- **Fix:** Created the test quest without finalizing it, and gave it a `ProposedDateEntity` on the target day instead, so the DM viewing their own not-yet-signed-up-for quest reaches the branch that actually renders `_Calendar`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs`
- **Verification:** Confirmed via source read of `Views/Quest/Details.cshtml` and `Details.Mobile.cshtml` that the calendar partial is reached with a non-finalized quest + proposed date; confirmed via `dotnet test` that the fact now fails at the correct assertion (the event-creation precondition) rather than passing silently.
- **Committed in:** `494211b` (Task 2 commit)

**2. [Rule 1 - Bug] Added precondition assertions so the suite is genuinely RED**
- **Found during:** Task 2, first `dotnet test` run
- **Issue:** All three `EventCalendarPartialTests.cs` facts passed on the first run, even though `/Events/Create` currently 404s. Since none of the three facts asserted anything about the event-creation POST's outcome, a failed (no-op) event creation left the "no event markup" and "quest creation unaffected" assertions trivially true — the suite reported 3/3 passing instead of the intended Wave 0 RED state.
- **Fix:** Added `eventCreateResponse.StatusCode.Should().Be(HttpStatusCode.Redirect)` immediately after each fact's `/Events/Create` POST.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs`
- **Verification:** Re-ran `dotnet test --filter FullyQualifiedName~EventCalendarPartialTests` — all 3 facts now fail as expected (RED), and `dotnet test --filter FullyQualifiedName~Event` across both files reports 13 failed / 0 passed, matching the plan's overall verification requirement.
- **Committed in:** `494211b` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bug fixes, both in test design rather than production code — no production code exists yet)
**Impact on plan:** Both fixes were necessary for the tests to actually test what the acceptance criteria describe. No scope creep — task boundaries, file list, and class/fact names are unchanged from the plan.

## Issues Encountered

None beyond the two deviations documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Both test files compile cleanly and are part of the solution; any later plan in this phase that lands `EventsController`, `EventEntity`, and the calendar wiring will turn these 13 facts green incrementally as each capability lands.
- No production code, migrations, or views were touched by this plan — the full production surface (entities, repository, domain, controller, views, calendar wiring) remains for later plans in this phase.
- The `EventBlockClass`/`EventChipClass` constants in `EventCalendarPartialTests.cs` (`"calendar-events"` / `"calendar-event"`) encode the UI-SPEC's locked CSS class names; a later plan implementing the calendar chip must use these exact class names or update both the CSS and this test's constants together.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-26*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs
- FOUND: .planning/phases/74-event-schema-crud-and-calendar-display/74-01-SUMMARY.md
- FOUND commit: 3657fb9 (Task 1)
- FOUND commit: 494211b (Task 2)
- FOUND commit: 21ed4f4 (docs: complete plan)
