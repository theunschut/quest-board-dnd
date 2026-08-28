---
phase: 76-recurring-event-series
plan: 09
subsystem: ui
tags: [aspnet-core-mvc, razor, automapper, bootstrap5, bootstrap-modal]

# Dependency graph
requires:
  - phase: 76-04
    provides: "IEventSeriesService (GetSeriesAsync, GetOccurrencesAsync, GetRemovalImpactAsync, EndAsync, DeleteAsync, DetachAsync) -- the single Domain orchestration point this plan's controller calls"
  - phase: 76-06
    provides: "The .cycle-mask-strip/.cycle-mask-cell/.text-purple CSS this plan's read-only rule card reuses, and the EventViewModel/ViewModelProfile shape this plan's own SeriesViewModels sit beside"
provides:
  - "SeriesController -- GET /Series/Details/{id} open to any board member, POST End/Delete/Detach gated behind the DM-only policy plus a per-action board re-check"
  - "SeriesDetailsViewModel/SeriesOccurrenceViewModel -- the read-only view models the series page renders from, with CadenceLabel and TimeLabel computed in one place each"
  - "Views/Series/Details.cshtml -- the series detail page reached from any occurrence, satisfying the /Series/Details/{id} route the 76-10 horizon banner already links to"
affects: [76-recurring-event-series (this closes out the last new surface the phase's UI-SPEC calls for)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SeriesController copies EventsController's SeriesIsOnActiveBoardAsync/IsDmTierAsync/GetEffectiveRoleAsync helpers verbatim in behaviour rather than extracting a shared base class, matching the plan's explicit choice to keep the two controllers independent"
    - "The removal-impact counts (PastCount/FutureCount/AnsweredCount) are only populated on the view model when the viewer is DM-tier -- a player's response never carries them, computed server-side rather than hidden by markup alone"
    - "The read-only cycle-mask strip reuses the exact CSS classes the Create form's interactive strip introduced (.cycle-mask-strip/.cycle-mask-cell/.cycle-mask-cell.on), rendered as non-button <span> elements with pointer-events: none and aria-disabled, so no new CSS was needed for the read-only variant"

key-files:
  created:
    - QuestBoard.Service/ViewModels/SeriesViewModels/SeriesDetailsViewModel.cs
    - QuestBoard.Service/ViewModels/SeriesViewModels/SeriesOccurrenceViewModel.cs
    - QuestBoard.Service/Controllers/Events/SeriesController.cs
    - QuestBoard.Service/Views/Series/Details.cshtml
  modified:
    - QuestBoard.Service/Automapper/ViewModelProfile.cs

key-decisions:
  - "CadenceLabel is worded literally as 'Every {N} week(s) on {Weekday}s' (plural weekday, literal 'week(s)') per the plan's explicit instruction, distinct from the Create form's client-side derived-weekday text which uses a singular weekday name -- the two surfaces word the same fact slightly differently by design, matching their respective specs."
  - "The Occurrences table's past/future divider is a single centered 'Today' row inserted the first time an occurrence's date is on or after today, reusing the same DateOnly.FromDateTime(DateTime.Today) comparison _Calendar.cshtml's own today marker uses, rather than a new visual language."
  - "The Delete Series confirm uses a Bootstrap modal (not a native confirm) with two destructive forms -- Detach and Delete -- each its own <form asp-action=...> so the tag helper's automatic antiforgery field covers both outcomes without any explicit token wiring in the view."

patterns-established:
  - "A read-only variant of an existing interactive widget (the cycle-mask strip) is built by re-rendering the same CSS classes on non-interactive elements rather than duplicating or parameterizing the original component -- future read-only reuses of the strip should follow the same approach."

requirements-completed: [EVTRECUR-03, EVTRECUR-04]

coverage:
  - id: D1
    description: "SeriesDetailsViewModel/SeriesOccurrenceViewModel exist with the fields the plan specifies, CyclePositions/CadenceLabel are computed from the Domain parser rather than a second parse, and the AutoMapper profile maps EventSeries -> SeriesDetailsViewModel and Event -> SeriesOccurrenceViewModel with no reverse map"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions confirm CyclePositions/Occurrences/CanManage/PastCount/FutureCount/AnsweredCount/CadenceLabel/TotalCount all present, zero DateTime.Parse/ToDateTime/new DateTime( occurrences"
        status: pass
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EntityProfileEnumCastTests -- 43/43 pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "GET /Series/Details/{id} is open to any board member and returns not-found for a cross-board id through the query filter; POST End/Delete/Detach all carry the DM-only policy, antiforgery validation, and re-resolve the series' owning board before writing"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "grep assertions: class preceded by [Authorize], End/Delete/Detach each preceded by [Authorize(Policy = \"DungeonMasterOnly\")] and [ValidateAntiForgeryToken]; file contains SeriesIsOnActiveBoardAsync and GetSeriesGroupIdAsync; zero IgnoreQueryFilters occurrences"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~EventTenantIsolationTests -- 8/8 pass"
        status: pass
    human_judgment: false
  - id: D3
    description: "The series page renders the rule and template read-only with no input, textarea or model-bound field anywhere, lists every occurrence with its cancelled state, and gates End/Delete behind CanManage"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "grep -cE 'asp-for=|<input type=\"text\"|<textarea' Views/Series/Details.cshtml -- 0; grep -c 'antiforgery|__RequestVerificationToken|asp-action' -- 3; literal-string checks for all six required copy strings pass"
        status: pass
      - kind: integration
        ref: "dotnet build (0 errors); dotnet test (full suite) -- QuestBoard.UnitTests 385/385, QuestBoard.IntegrationTests 498/498"
        status: pass
    human_judgment: false
  - id: D4
    description: "Ending a series sets an end date and clears future occurrences in one confirmed action while always keeping past sessions; removing offers delete-everything or detach with the exact blast radius stated first"
    requirement: "EVTRECUR-04"
    verification:
      - kind: manual_procedural
        ref: "grep: End action calls EndAsync(id, DateOnly.FromDateTime(DateTime.Today), removeFutureOccurrences: true, token); Delete action calls DeleteAsync; Detach action calls DetachAsync; the confirm/modal copy locals interpolate PastCount/FutureCount/AnsweredCount/TotalCount computed by the controller from GetRemovalImpactAsync"
        status: pass
    human_judgment: true
    rationale: "The end confirm and the two-outcome removal modal are interactive dialogs whose exact wording and button wiring can be confirmed by grep, but whether they read correctly and behave as expected in a real browser session (a DM actually ending or removing a series with real occurrences) needs a human or browser-driving UAT pass."

# Metrics
duration: ~20min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 09: Series Detail Page and Lifecycle Controls Summary

**A new `SeriesController` and read-only `Views/Series/Details.cshtml` give a DM the one place a recurring series can be inspected, ended, or removed -- reached from any occurrence, gated behind the DM policy plus a per-action board re-check, and satisfying the `/Series/Details/{id}` route the calendar's horizon banner already links to.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-28T16:05:00+02:00 (approx.)
- **Completed:** 2026-08-28T16:25:00+02:00
- **Tasks:** 3
- **Files modified:** 5 (4 created, 1 modified)

## Accomplishments

- `SeriesOccurrenceViewModel` and `SeriesDetailsViewModel` in a new `QuestBoard.Service.ViewModels.SeriesViewModels` namespace, both with computed `TimeLabel` (worded rather than blank), and `SeriesDetailsViewModel` additionally computing `CadenceLabel` and `TotalCount` in one place each so the header and rule card cannot drift.
- `ViewModelProfile` gained `CreateMap<EventSeries, SeriesDetailsViewModel>()` (ignoring the six controller-filled members) and `CreateMap<Event, SeriesOccurrenceViewModel>()` (convention-mapped, including the computed `IsCancelled`), with no reverse map on either -- the page is read-only.
- `SeriesController` serves `GET Details` to any board member (not-found for a cross-board id through the query filter), and gates `POST End`, `POST Delete` and `POST Detach` behind the DM-only policy, antiforgery validation, and a copied `SeriesIsOnActiveBoardAsync` second-layer board check. `End` calls `EndAsync` with today's date and `removeFutureOccurrences: true`; `Delete`/`Detach` call `DeleteAsync`/`DetachAsync` directly.
- `Views/Series/Details.cshtml` renders three stacked cards: a read-only Recurrence Rule card (anchor date, cadence label, the reused-CSS read-only cycle-mask strip, end date or "No end date -- runs indefinitely.", and the template rendered through the shared Markdown helper), an Occurrences card (a table with a centered "Today" divider row and a `bg-secondary` "Cancelled" badge plus struck-through row text for cancelled occurrences), and a manager-only Actions card offering "End Series" (native confirm, branching on whether any answers exist) and "Delete Series" (opens a Bootstrap modal with "Detach sessions" and "Delete everything" as two separate posting forms, plus a dismissing "Cancel").

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the series view models and their mapping** - `10f3a5d` (feat)
2. **Task 2: Create SeriesController with Details, End, Delete and Detach** - `261528a` (feat)
3. **Task 3: Build the series Details view with the read-only rule, occurrence table and lifecycle controls** - `662c437` (feat)

**Plan metadata:** committed alongside this SUMMARY (worktree mode -- orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.Service/ViewModels/SeriesViewModels/SeriesOccurrenceViewModel.cs` - New: `Id`, `Date`, `StartTime`, `IsCancelled`, computed `TimeLabel`
- `QuestBoard.Service/ViewModels/SeriesViewModels/SeriesDetailsViewModel.cs` - New: rule fields, `CyclePositions`, `Occurrences`, `CanManage`, the three removal-impact counts, computed `CadenceLabel`/`TimeLabel`/`TotalCount`
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Added `CreateMap<EventSeries, SeriesDetailsViewModel>()` and `CreateMap<Event, SeriesOccurrenceViewModel>()`
- `QuestBoard.Service/Controllers/Events/SeriesController.cs` - New: `Details`, `End`, `Delete`, `Detach`, and the copied board/role helpers
- `QuestBoard.Service/Views/Series/Details.cshtml` - New: the three-card read-only series page

## Decisions Made

- `CadenceLabel` is worded "Every {N} week(s) on {Weekday}s" (literal "week(s)", plural weekday) exactly as the plan specifies, which reads slightly differently from the Create form's client-side derived-weekday text (singular weekday) -- both are correct per their own governing spec, and neither was changed to match the other.
- The Occurrences table's past/future split is a single "Today" divider row rendered the first time an occurrence's date is on or after `DateOnly.FromDateTime(DateTime.Today)`, matching `_Calendar.cshtml`'s own today-marker comparison rather than inventing new past/future styling.
- The removal modal's two forms (`Detach`, `Delete`) each carry their own `asp-action`, letting the FormTagHelper's automatic antiforgery field cover both without any explicit token markup in the view -- matching the existing `Events/Details.cshtml` Delete form's own pattern of relying on the tag helper rather than writing `__RequestVerificationToken` by hand.

## Deviations from Plan

None - plan executed exactly as written. All three tasks' acceptance criteria (grep assertions for interface/view shape, exact copy strings, zero editable-field markers, `dotnet build` exit 0, the two named test-filter runs, and the full test suite) were verified directly.

## Issues Encountered

None. `IActiveGroupContext.RequireActiveGroupId()` required an explicit `using QuestBoard.Domain.Extensions;` (it is an extension method, not an interface member) -- caught immediately by the first build and added before the task's acceptance criteria were checked; not counted as a deviation since it is a straightforward missing-using compile fix within the same task, not a change to the plan's design.

## User Setup Required

None - no external service configuration required. No migration in this plan.

## Next Phase Readiness

- `/Series/Details/{id}` now exists and resolves, closing the gap the 76-10 horizon banner's `Url.Action("Details", "Series", ...)` link was written against before this controller existed.
- The two interactive dialogs (the end confirm's branching copy, and the two-outcome removal modal) are wired and grep-verified but have not been exercised in a real browser session with real occurrence/answer counts -- this is the one remaining UAT item for this plan, consistent with how 76-06 and 76-10 already flagged their own interactive/visual surfaces for the same reason.
- No blockers for merge -- this plan touched only its declared files (`Controllers/Events/SeriesController.cs`, `ViewModels/SeriesViewModels/`, `Views/Series/Details.cshtml`, `Automapper/ViewModelProfile.cs`) and did not modify any file owned by the concurrently-running 76-07 plan.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/ViewModels/SeriesViewModels/SeriesDetailsViewModel.cs
- FOUND: QuestBoard.Service/ViewModels/SeriesViewModels/SeriesOccurrenceViewModel.cs
- FOUND: QuestBoard.Service/Controllers/Events/SeriesController.cs
- FOUND: QuestBoard.Service/Views/Series/Details.cshtml
- FOUND: commit 10f3a5d (Task 1)
- FOUND: commit 261528a (Task 2)
- FOUND: commit 662c437 (Task 3)
