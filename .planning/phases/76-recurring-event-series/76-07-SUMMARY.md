---
phase: 76-recurring-event-series
plan: 07
subsystem: ui
tags: [aspnet-core-mvc, razor, bootstrap5, authorization]

# Dependency graph
requires:
  - phase: 76-04
    provides: "IEventService.SetCancelledAsync -- the narrow write path this plan's Cancel/Restore actions call"
  - phase: 76-06
    provides: "EventViewModel.SeriesId/CancelledAt/IsCancelled and the shipped EventsController with SeriesIsOnActiveBoardAsync's second-layer board check"
provides:
  - "EventsController.Cancel and .Restore -- DM-only, antiforgery-protected, re-resolving SeriesId and board ownership server-side before writing CancelledAt"
  - "EventsController.Delete refusing any event carrying a SeriesId, independent of which button the browser rendered"
  - "Occurrence Details page: cancelled banner, availability controls suppressed (not disabled) when cancelled, Cancel/Restore replacing Delete for series members, and a View Series Details link for every series occurrence"
affects: [76-09 (Series Details page's own Details action is the link target this plan's View Series Details button points at)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cancel/Restore follow the same POST-time re-resolution precedent as QuestController.Close/Reopen -- the condition is checked on the server action itself, never inferred from which button rendered"
    - "Confirm-dialog copy containing an apostrophe is interpolated into a backtick-delimited JS string inside a double-quoted HTML attribute, avoiding the single-quote collision the existing deleteConfirmMessage pattern never had to handle"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Views/Events/Details.cshtml

key-decisions:
  - "The 'You haven't answered yet. Choosing an answer below signs you up.' message is swapped for 'You did not answer before this session was cancelled.' when Model.IsCancelled and the user has no signup -- the original copy references buttons 'below' that no longer render once cancelled, so leaving it verbatim would read as a dangling reference to controls the page no longer offers (Rule 1 fix, scoped to the exact lines this task touches)."
  - "The Cancel/Restore confirm() calls use backtick JS string delimiters instead of the existing single-quote convention, because the UI-SPEC's mandated cancel copy contains a literal apostrophe ('it's off') that would otherwise terminate a single-quoted JS string once Razor's HTML-encode-then-browser-decode round trip restores the raw character inside the attribute value."

requirements-completed: [EVTRECUR-04]

coverage:
  - id: D1
    description: "EventsController.Cancel and .Restore exist, DM-only and antiforgery-protected, re-resolve SeriesId and SeriesIsOnActiveBoardAsync on the POST, and call SetCancelledAsync with DateTime.UtcNow / null respectively"
    requirement: "EVTRECUR-04"
    verification:
      - kind: manual_procedural
        ref: "grep: EventsController.cs contains 'public async Task<IActionResult> Cancel(int id' and 'public async Task<IActionResult> Restore(int id', both preceded by [Authorize(Policy = \"DungeonMasterOnly\")] and [ValidateAntiForgeryToken], SetCancelledAsync(id, DateTime.UtcNow and SetCancelledAsync(id, null both present, SeriesIsOnActiveBoardAsync count = 4"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~EventTenantIsolationTests -- 8/8 pass"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors"
        status: pass
    human_judgment: false
  - id: D2
    description: "Delete refuses any event with a SeriesId with a bad request, leaving the row unchanged; a one-off event's Delete path is byte-for-byte unchanged"
    requirement: "EVTRECUR-04"
    verification:
      - kind: manual_procedural
        ref: "grep -F 'Delete is not supported for an occurrence of a recurring series.' EventsController.cs -- present"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors"
        status: pass
    human_judgment: false
  - id: D3
    description: "Details.cshtml renders an alert-secondary cancelled banner with fa-ban above the Your Availability card, and removes (not disables) the Yes/Maybe/No buttons and Withdraw control when Model.IsCancelled, while the roster and current-answer line stay visible"
    verification:
      - kind: manual_procedural
        ref: "grep: Details.cshtml contains Model.IsCancelled x4, alert-secondary, fa-ban x2, literal 'This session has been cancelled.'; git diff shows zero removed lines containing setAvailability"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors; dotnet test (full suite) -- 883/883 pass"
        status: pass
    human_judgment: true
    rationale: "Grep and dotnet test confirm the markup exists, is wired to the right model flag, and did not remove the existing availability script -- but whether the banner and control suppression actually renders and reads correctly in a browser at both desktop and mobile widths needs a human or browser-driving UAT pass, matching 76-06's precedent for this page's other interactive markup."
  - id: D4
    description: "The Actions card shows a View Series Details link for every series occurrence, and Cancel Occurrence (btn-outline-warning) or Restore Occurrence (btn-outline-success) in place of Delete for a series member depending on cancelled state, while a one-off event keeps Delete Event unchanged"
    verification:
      - kind: manual_procedural
        ref: "grep: Details.cshtml contains literal 'Cancel Occurrence', 'View Series Details', 'Delete Event', fa-repeat, fa-undo, btn-outline-warning count=1, deleteConfirmMessage count=2 (still present)"
        status: pass
      - kind: other
        ref: "dotnet build -- 0 errors; dotnet test (full suite) -- 883/883 pass"
        status: pass
    human_judgment: true
    rationale: "The three-way Delete/Cancel/Restore branch and its confirm-dialog copy are visual/interaction behavior that grep and dotnet test can confirm exists and targets the right actions, but cannot confirm the branch renders correctly for an actual cancelled vs. live series occurrence in a real browser."

# Metrics
duration: ~15min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 07: Occurrence Cancel/Restore and Delete Refusal Summary

**Occurrence Details gained Cancel/Restore actions with a server-side Delete refusal for series occurrences, plus a cancelled banner, action suppression, and a View Series Details link on the Details view.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-28T13:52:00Z (approx.)
- **Completed:** 2026-08-28T14:08:08Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- `EventsController.Cancel` and `.Restore` are DM-only, antiforgery-protected actions that load the event, re-resolve `SeriesId` on the POST, re-check the series against the active board via the existing `SeriesIsOnActiveBoardAsync` second-layer check, then call `eventService.SetCancelledAsync` with `DateTime.UtcNow` or `null` respectively -- un-cancelling is a single lossless write, matching the tombstone design shipped in an earlier plan.
- `EventsController.Delete` now rejects any event carrying a `SeriesId` with a bad request before removal, so a hard delete against a series occurrence is refused regardless of what the browser rendered; a one-off event's delete path is untouched.
- `Details.cshtml` renders an `alert-secondary` cancelled banner (`fa-ban`, "This session has been cancelled.") immediately above the "Your Availability" card when `Model.IsCancelled`, and removes the Yes/Maybe/No buttons and the Withdraw control entirely (not disabled) for a cancelled occurrence, while the roster and the "Your current answer" line stay visible as historical record.
- The Actions card gained a "View Series Details" link (`fa-repeat`, `btn-outline-secondary`) for every series occurrence, and a Cancel Occurrence (`btn-outline-warning`, `fa-ban`) or Restore Occurrence (`btn-outline-success`, `fa-undo`) control in place of Delete for a series member, depending on cancelled state; a one-off event keeps "Delete Event" exactly as shipped.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Cancel and Restore actions and make Delete refuse a series occurrence server-side** - `8ed830f` (feat)
2. **Task 2: Render the cancelled banner, swap Delete for Cancel, and link to the series** - `ee6626c` (feat)

**Plan metadata:** committed alongside this SUMMARY (worktree mode -- orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Added `Cancel` and `Restore` actions; `Delete` now refuses any event with a `SeriesId`
- `QuestBoard.Service/Views/Events/Details.cshtml` - Cancelled banner, availability-control suppression when cancelled, View Series Details link, Cancel/Restore replacing Delete for series occurrences

## Decisions Made

- The "haven't answered yet" copy is swapped to a cancelled-aware variant when `Model.IsCancelled` and the user has no signup, since the shipped text references answer buttons "below" that no longer render once cancelled (Rule 1 auto-fix, scoped strictly to the lines this task already touches).
- Cancel/Restore's `confirm()` calls use backtick-delimited JS strings rather than the codebase's existing single-quote convention, because the UI-SPEC's mandated cancel copy contains a literal apostrophe that would otherwise terminate a single-quoted JS string after Razor's HTML-encode/browser-decode round trip restores the raw character inside the attribute.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Reworded the no-signup availability message for a cancelled occurrence**
- **Found during:** Task 2
- **Issue:** The shipped "You haven't answered yet. Choosing an answer below signs you up." text references buttons that Task 2 removes entirely when the occurrence is cancelled, leaving a dangling reference to controls that no longer exist on the page.
- **Fix:** Added an `else if (Model.IsCancelled)` branch rendering "You did not answer before this session was cancelled." instead, leaving the existing branch untouched for the live-occurrence case.
- **Files modified:** QuestBoard.Service/Views/Events/Details.cshtml
- **Verification:** `dotnet build` (0 errors), full test suite (883/883 pass); grep confirms both branches are present and mutually exclusive.
- **Committed in:** ee6626c (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary for correctness -- the alternative left a UI copy bug directly caused by this task's own control removal. No scope creep beyond the two lines touched.

## Issues Encountered

None. `dotnet build` succeeded with 0 errors on both tasks, and the full test suite (`QuestBoard.UnitTests` 385/385, `QuestBoard.IntegrationTests` 498/498 -- 883/883 total) passed unchanged.

## User Setup Required

None - no external service configuration required. No migration in this plan (schema and `SetCancelledAsync` already shipped in an earlier plan).

## Next Phase Readiness

- The "View Series Details" link targets `Url.Action("Details", "Series", new { id = Model.SeriesId })`, resolving once the parallel 76-09 plan's `SeriesController.Details` action merges -- no `SeriesController` stub was added here per the parallel-execution boundary.
- Cancel/Restore and the Delete refusal are ready for the Series Details page's own occurrence list (76-09) to link back into.
- No blockers.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/Events/EventsController.cs
- FOUND: QuestBoard.Service/Views/Events/Details.cshtml
- FOUND: commit 8ed830f (Task 1)
- FOUND: commit ee6626c (Task 2)
