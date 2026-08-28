---
phase: 76-recurring-event-series
plan: 10
subsystem: ui
tags: [razor, bootstrap, calendar, mvc, dm-only-banner]

# Dependency graph
requires:
  - phase: 76-04
    provides: "IEventSeriesService (GetSeriesBelowRunwayAsync, GetActiveSeriesForActiveGroupAsync) registered in DI, SeriesRunwayStatus projection"
provides:
  - "CalendarViewModel.SeriesBelowRunway / CanManage — safe-by-default (empty/false) additions consumed only by Calendar/Index.cshtml"
  - "CalendarController now DM-gates the runway query so a player never triggers it and never receives series titles"
  - ".calendar-event.cancelled / .agenda-event-entry.cancelled / .legend-item.cancelled — the cancelled visual state on both calendar surfaces plus the Legend"
  - "The DM-gated horizon banner on the calendar page, linking each under-runway series to its series details page"
affects: [76-recurring-event-series (Series Details page and mobile UAT still to verify against a real mobile user agent)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "The shared _Calendar.cshtml partial's protection mechanism (collections default empty, so the 5 unrelated call sites render nothing) was extended rather than replaced: the new cancelled modifier is a conditional CSS class on an element already gated by day.EventsOnDay.Any(), and the horizon banner was deliberately placed in Calendar/Index.cshtml's own markup instead of the partial so the same 5 call sites remain structurally incapable of rendering it."

key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.Service/wwwroot/css/calendar.mobile.css

key-decisions:
  - "The horizon banner links each series title to /Series/Details/{id} via Url.Action(\"Details\", \"Series\", ...) even though SeriesController (built in the parallel 76-09 plan of the same wave) does not yet exist in this worktree. Url.Action only builds a route string at render time and has no compile-time dependency on the target controller existing, so the build and full test suite both pass; the route will resolve once 76-09 merges in the same wave."
  - "The legend's Cancelled swatch reuses the event swatch's purple border-left-color (#6f42c1) with opacity: 0.55 layered on top, rather than a new hue — matching the UI-SPEC's 'same purple chip, faded' rule for the calendar/agenda cancelled state."

requirements-completed: [EVTRECUR-03, EVTRECUR-04]

coverage:
  - id: D1
    description: "Cancelled occurrences render faded and struck through on the desktop calendar chip and the mobile agenda entry, with no new flag added to the shared partial"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions confirm _Calendar.cshtml gained no ViewBag flag and no alert- markup"
        status: pass
    human_judgment: true
    rationale: "The plan explicitly requires verifying this markup against a real mobile user agent, not devtools emulation, before the phase's acceptance closes — that check cannot be performed from this worktree and must happen during UAT."
  - id: D2
    description: "The Legend gains a Cancelled row so the calendar still explains its own visual states"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "grep -c 'legend-item' QuestBoard.Service/Views/Calendar/Index.cshtml == 4; .legend-item.cancelled present in calendar.css"
        status: pass
    human_judgment: false
  - id: D3
    description: "A DM sees a horizon banner on the calendar naming every series running low on upcoming sessions, with a link to each series page; a player sees nothing"
    requirement: "EVTRECUR-04"
    verification:
      - kind: unit
        ref: "grep assertions on Calendar/Index.cshtml for Model.CanManage, Model.SeriesBelowRunway, alert-warning, and the literal 'is running low'; dotnet test (878/878 pass)"
        status: pass
    human_judgment: true
    rationale: "Requires signing in as a DM with a real below-runway series and as a player on the same board to visually confirm the banner appears for one and not the other — genuine UAT, not something a grep can prove."
  - id: D4
    description: "The five protected call sites of the shared _Calendar.cshtml partial remain untouched in behaviour"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "grep -c 'alert-warning' QuestBoard.Service/Views/Shared/_Calendar.cshtml == 0; grep -c 'ViewBag' (diff-added lines) == 0"
        status: pass
    human_judgment: false

# Metrics
duration: 10min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 10: Cancelled State and Horizon Banner on the Calendar Summary

**DM-gated horizon banner and a faded/struck-through cancelled modifier on both calendar surfaces, wired through a safe-by-default CalendarViewModel extension**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-08-28T15:50:00+02:00 (approx, base commit a820e48)
- **Completed:** 2026-08-28T15:54:14+02:00
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- `CalendarViewModel` gained `SeriesBelowRunway` (empty by default) and `CanManage` (false by default), matching the existing `Events` collection's "safe default protects the shared partial's other callers" pattern
- `CalendarController` now injects `IEventSeriesService`, `IUserService`, `IActiveGroupContext` and only queries `GetSeriesBelowRunwayAsync` when the viewer is DM-tier, using the same `IsDmTierAsync`/`GetEffectiveRoleAsync` shape (including the SuperAdmin shortcut) already shipped on `EventsController`
- A cancelled occurrence now renders faded (`opacity: 0.55`) and struck through on both the desktop calendar chip (`.calendar-event.cancelled`) and the mobile agenda entry (`.agenda-event-entry.cancelled`), staying clickable through to Details exactly as before
- The DM-gated horizon banner renders in `Calendar/Index.cshtml` only, above the month grid, with locked copy for the single-series and multiple-series cases, each series title linked to its series details page
- The Legend card gained a fourth row — a `.legend-item.cancelled` swatch (same purple border, `opacity: 0.55`) and the label "Cancelled" — so all four calendar visual states are now explained in one place

## Task Commits

Each task was committed atomically:

1. **Task 1: Carry the runway status and manager flag on the calendar view model and controller** - `b337ad7` (feat)
2. **Task 2: Add the cancelled chip and agenda modifiers with their styles** - `64dde6a` (feat)
3. **Task 3: Add the DM-gated horizon banner and the Cancelled legend row to the calendar page** - `90b0e7d` (feat)

_No TDD tasks in this plan; no plan-metadata commit in worktree mode (STATE.md/ROADMAP.md are excluded — the orchestrator updates them after merge)._

## Files Created/Modified
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs` - Added `SeriesBelowRunway` and `CanManage`, both defaulting empty/false
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - Injected `IEventSeriesService`/`IUserService`/`IActiveGroupContext`; added `IsDmTierAsync`/`GetEffectiveRoleAsync`; DM-gated the runway query
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` - Added a conditional `cancelled` class to the existing desktop chip element only
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - Added the same conditional `cancelled` class to the mobile agenda entry
- `QuestBoard.Service/Views/Calendar/Index.cshtml` - Added the DM-gated horizon banner and the Cancelled legend row
- `QuestBoard.Service/wwwroot/css/calendar.css` - Added `.calendar-event.cancelled` and `.legend-item.cancelled`
- `QuestBoard.Service/wwwroot/css/calendar.mobile.css` - Added `.agenda-event-entry.cancelled`

## Decisions Made
- The horizon banner's series links target `Url.Action("Details", "Series", new { id = series.SeriesId })`. `SeriesController` is built by the parallel 76-09 plan in the same wave and does not exist in this worktree at execution time; `Url.Action` builds the route string without validating the target controller at compile time, so this does not block the build or test suite, and the route resolves once both plans merge.
- The Cancelled legend swatch reuses the event swatch's purple border color with `opacity: 0.55` layered on, per the UI-SPEC's "same chip, faded" rule — no new hue introduced.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The calendar and mobile agenda surfaces are fully wired for the cancelled state and the horizon banner; both are covered by `dotnet build` (0 errors) and the full test suite (878/878 passing).
- Two items require live verification once all wave-5 plans merge and the app can run end to end: (1) the mobile agenda's cancelled modifier against a real mobile user agent, not devtools emulation, per the plan's explicit requirement; (2) the horizon banner and series links against a real DM session with an actual below-runway series, once `SeriesController`/`/Series/Details/{id}` lands from 76-09.
- No blockers for merge — this plan touched only its declared files (`Views/Shared/_Calendar.cshtml`, `Views/Calendar/*`, `Controllers/QuestBoard/CalendarController.cs`, the Calendar view model, and `wwwroot/css/calendar*.css`) and did not modify any file owned by the concurrently-running 76-05 or 76-06 plans.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

All 8 declared files found on disk; all 4 commit hashes (`b337ad7`, `64dde6a`, `90b0e7d`, `cd46f64`) found in git log.
