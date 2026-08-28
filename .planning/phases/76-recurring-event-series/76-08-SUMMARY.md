---
phase: 76-recurring-event-series
plan: 08
subsystem: ui
tags: [aspnet-core-mvc, razor, bootstrap5, authorization]

# Dependency graph
requires:
  - phase: 76-04
    provides: "IEventSeriesService.ApplyTemplateToFutureAsync and CountLiveSiblingsOnDateAsync -- the propagation and collision-count methods this plan wires into the Edit POST and the new collision endpoint"
  - phase: 76-06
    provides: "EventViewModel.EditScope/SeriesId and the shipped EventsController.Edit POST this plan extends"
  - phase: 76-07
    provides: "EventsController's SeriesIsOnActiveBoardAsync second-layer board check, reused unchanged by the new collision endpoint"
provides:
  - "EventsController.Edit POST branching on EditScope: OnlyThisEvent behaves exactly as before, ThisAndFutureEvents sweeps the series template via ApplyTemplateToFutureAsync after the single-event save and rejects a one-off event with a bad request"
  - "EventsController.CheckOccurrenceCollision -- DM-only, antiforgery-protected advisory read counting live siblings on a target date via CountLiveSiblingsOnDateAsync"
  - "Edit.cshtml's two-button save-scope modal (Only this event / This and all future events) intercepting Save Changes for a series occurrence, with an inline collision notice fed by the new endpoint; a one-off event's form is untouched"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "The scope dialog only calls the collision-check endpoint when the Date field's value differs from a hidden original-date field, avoiding an unnecessary round trip when the date did not change"
    - "A failed collision-check fetch opens the modal anyway with no warning strip -- an advisory lookup failing must never block a save"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Views/Events/Edit.cshtml

key-decisions:
  - "The EditScope hidden field defaults to OnlyThisEvent (the enum's default value) so a form submitted without the scope dialog running -- should the click interceptor ever fail to attach -- falls back to the safe single-event save rather than silently sweeping the series."
  - "The custom two-button Bootstrap modal is used instead of the app's native confirm() precedent, per the UI-SPEC's explicit call-out that this prompt has two affirmative outcomes and a native dialog is binary."

requirements-completed: [EVTRECUR-05, EVTRECUR-06]

coverage:
  - id: D1
    description: "The Edit POST honours EditScope: OnlyThisEvent keeps the exact existing single-event update and toast, ThisAndFutureEvents additionally calls ApplyTemplateToFutureAsync and swaps the toast wording, and a future-scope post against a one-off event (no SeriesId) returns a bad request"
    requirement: "EVTRECUR-05"
    verification:
      - kind: manual_procedural
        ref: "grep: EventsController.cs contains 'EventEditScope.ThisAndFutureEvents', 'ApplyTemplateToFutureAsync', both toast literals ('Event updated successfully.' and 'This event and all future sessions in the series were updated.')"
        status: pass
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EventSeriesMaterializationTests -- 14/14 pass"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors; dotnet test (full suite) -- 883/883 pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "CheckOccurrenceCollision is DM-only and antiforgery-protected, returns no-collision Json for a one-off event, re-checks the series against the active board, and reports live (non-cancelled) siblings on a date via CountLiveSiblingsOnDateAsync without ever blocking the save"
    requirement: "EVTRECUR-06"
    verification:
      - kind: manual_procedural
        ref: "grep: EventsController.cs contains 'public async Task<IActionResult> CheckOccurrenceCollision(int id, DateOnly date' preceded by [Authorize(Policy = \"DungeonMasterOnly\")] and [ValidateAntiForgeryToken], and 'CountLiveSiblingsOnDateAsync'"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors"
        status: pass
    human_judgment: false
  - id: D3
    description: "Editing a series occurrence intercepts Save Changes and opens a two-button scope modal (Only this event / This and all future events) with a collision notice fed from the check endpoint; a one-off event's form submits directly, unchanged; no cadence/anchor/mask fields render on this form under any condition"
    verification:
      - kind: manual_procedural
        ref: "grep: Edit.cshtml contains 'Model.SeriesId != null' x3, 'EditScope' x4, 'modal' x18, '/Events/CheckOccurrenceCollision' x1, literal 'Save this change', 'Only this event', 'This and all future events', 'Saving will not merge or block it'; grep -cE 'CycleMask|IntervalWeeks|AnchorDate|SeriesEndDate' returns 0; git diff shows zero removed asp-for=\"Title\" lines"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors; dotnet test (full suite) -- 883/883 pass"
        status: pass
    human_judgment: true
    rationale: "Grep and dotnet test confirm the markup, hidden fields and script wiring exist and target the right endpoints, but whether the modal actually opens, focuses the right button, and displays the collision strip correctly in a real browser needs a human or browser-driving UAT pass, matching this phase's precedent for its other interactive markup (76-06, 76-07)."

# Metrics
duration: ~25min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 08: Occurrence Edit Scope and Collision Notice Summary

**Editing a series occurrence now prompts for save scope (only this event vs. this and every future session) in a custom two-button modal, wired to a new DM-only collision-check endpoint that surfaces a non-blocking notice when the moved date already holds a live sibling.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-28T13:56:00Z (approx.)
- **Completed:** 2026-08-28T14:20:51Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- `EventsController.Edit` (POST) still performs the existing single-event update in every case, then branches on `viewModel.EditScope`: `OnlyThisEvent` keeps the exact existing toast and behavior; `ThisAndFutureEvents` additionally calls `eventSeriesService.ApplyTemplateToFutureAsync` to sweep the series template (title, description, start time only -- never the edited occurrence's own date) onto untouched future occurrences, and returns a bad request if the target event carries no `SeriesId`.
- `EventsController.CheckOccurrenceCollision` is a new DM-only, antiforgery-protected `POST` action that loads the event, returns a no-collision `Json` result for a one-off event, re-verifies the series against the active board, then reports whether `eventSeriesService.CountLiveSiblingsOnDateAsync` finds a live sibling already on the target date -- the check is advisory only and never blocks a save.
- `Edit.cshtml` intercepts the Save Changes click for a series occurrence (`Model.SeriesId != null`) and opens a custom two-button Bootstrap modal ("Only this event" / "This and all future events") instead of submitting directly; it calls the new collision endpoint only when the Date field actually changed, shows an `alert-warning` strip with the exact UI-SPEC wording when a live sibling collides, and falls back to opening the modal with no strip if the check itself fails. A one-off event's form is completely unchanged -- same fields, same direct submit, no modal, no cadence field anywhere on this view.

## Task Commits

Each task was committed atomically:

1. **Task 1: Make the Edit POST scope-aware and add the collision-check endpoint** - `ec6f71f7` (feat)
2. **Task 2: Add the save-scope modal and collision notice to the edit form** - `20250fbb` (feat)

**Plan metadata:** committed alongside this SUMMARY (worktree mode -- orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.Service/Controllers/Events/EventsController.cs` - `Edit` POST branches on `EditScope`; new `CheckOccurrenceCollision` action
- `QuestBoard.Service/Views/Events/Edit.cshtml` - Save-scope modal, collision-notice strip, and the click-intercept/fetch script for series occurrences only

## Decisions Made

- The `EditScope` hidden field defaults to `OnlyThisEvent` so an edge case where the modal's click interceptor never attaches still falls back to the safe single-event save rather than silently sweeping the series.
- Used a custom two-button modal rather than the app's usual native `confirm()`, per the UI-SPEC's explicit rationale that this prompt has two affirmative outcomes and a native dialog is binary.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. `dotnet build` succeeded with 0 errors on both tasks, and the full test suite (`QuestBoard.UnitTests` 385/385, `QuestBoard.IntegrationTests` 498/498 -- 883/883 total) passed unchanged.

## User Setup Required

None - no external service configuration required. No migration in this plan (schema already shipped in an earlier plan).

## Next Phase Readiness

- Both `EVTRECUR-05` and `EVTRECUR-06` are implemented and covered by the existing materialization test suite plus the full regression pass.
- The collision endpoint and scope-aware Edit POST are ready for a browser-driven UAT pass to confirm the modal's visual behavior (focus order, collision strip rendering) matches the UI-SPEC.
- No blockers.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/Events/EventsController.cs
- FOUND: QuestBoard.Service/Views/Events/Edit.cshtml
- FOUND: commit ec6f71f7 (Task 1)
- FOUND: commit 20250fbb (Task 2)
