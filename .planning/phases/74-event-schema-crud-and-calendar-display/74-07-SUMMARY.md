---
phase: 74-event-schema-crud-and-calendar-display
plan: 07
subsystem: ui
tags: [aspnet-mvc, razor, css, calendar, mobile]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 06)
    provides: EventOnDay view model (Event, IsAllDay, TimeLabel), CalendarViewModel.Events, CalendarDay.EventsOnDay
affects: [74-08, 75-event-availability]
provides:
  - Mobile agenda day filter widened to include days with events, not only quests
  - Mobile agenda event entries rendered above quest entries within each day, reading EventOnDay.TimeLabel
  - Month-neutral empty state ("Nothing This Month") replacing the quest-only empty state
  - .agenda-event-entry / .agenda-event-title mobile CSS mirroring the quest entry, accented in #6f42c1

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mobile agenda entry mirrors the desktop chip's data source (EventOnDay.TimeLabel) and its purple #6f42c1 accent, so the all-day wording and colour cannot drift between platforms"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.mobile.css

key-decisions:
  - "None yet — Tasks 1 and 2 executed exactly as planned. Plan is paused at the Task 3 human-verify checkpoint pending developer confirmation on a real mobile User-Agent."

patterns-established: []

requirements-completed: []

# Requirement EVENT-04 is not yet marked complete: the plan's own checkpoint (Task 3)
# requires developer confirmation under a genuine mobile User-Agent before this plan closes.

coverage:
  - id: D1
    description: "A day that has an event but no quest appears in the mobile agenda, which previously listed only days with quests"
    requirement: "EVENT-04"
    verification:
      - kind: manual_procedural
        ref: "Pending Task 3 human-verify checkpoint — developer must confirm under a genuine mobile User-Agent"
        status: unknown
    human_judgment: true
    rationale: "This codebase has a live precedent of mobile markup that was never actually selected by the platform view switch, so devtools viewport emulation is not accepted as verification — a real mobile User-Agent check is required per the plan's own Task 3 design."
  - id: D2
    description: "Events render before quests within a day section, and an event with no start time always shows the all-day wording rather than a blank time slot"
    requirement: "EVENT-04"
    verification:
      - kind: manual_procedural
        ref: "Pending Task 3 human-verify checkpoint"
        status: unknown
    human_judgment: true
    rationale: "Same as D1 — requires a real mobile User-Agent check, not automatable."
  - id: D3
    description: "The empty state is month-neutral rather than claiming a month with events has no quests"
    requirement: "EVENT-04"
    verification:
      - kind: integration
        ref: "grep confirms 'No Quests This Month' and 'No adventures are planned' no longer appear in Index.Mobile.cshtml; 'Nothing This Month' and 'No quests or events are planned for' each appear exactly once"
        status: pass
    human_judgment: false
  - id: D4
    description: "A mobile event entry taps through to the same event details view as the desktop chip"
    requirement: "EVENT-04"
    verification:
      - kind: integration
        ref: "grep confirms Url.Action(\"Details\", \"Events\", ...) appears exactly once in the event entry"
        status: pass
    human_judgment: false

# Metrics
duration: 25min
completed: 2026-08-27
status: in-progress
---

# Phase 74 Plan 07: Mobile Calendar Agenda Event Rendering Summary

**Mobile agenda widened to list days with events (not only quests), with event entries rendered above quest entries reading the same TimeLabel property as the desktop chip, and a month-neutral empty state — Tasks 1-2 complete and committed, Task 3 human-verify checkpoint pending.**

## Performance

- **Duration:** 25 min (Tasks 1-2; Task 3 checkpoint not yet resolved)
- **Started:** 2026-08-27T00:00:00Z (approx)
- **Tasks:** 2/3 complete (Task 3 is a blocking human-verify checkpoint)
- **Files modified:** 2

## Accomplishments
- `agendaDays` filter in `Index.Mobile.cshtml` now qualifies a day when it has quests OR events, not quests alone
- Event entries render immediately after the day label and before the quest entry loop, all events with no per-day cap, mirroring the desktop day cell's ordering
- Each event entry reads `eventOnDay.TimeLabel` for its right-hand slot, so the all-day wording can never diverge from the desktop chip's wording
- Event entry navigates to `/Events/Details/{id}` via the same `Url.Action("Details", "Events", ...)` pattern the desktop chip uses
- Empty-state copy rewritten to be month-neutral: heading `Nothing This Month`, body `No quests or events are planned for {month}. Check another month.` — the old quest-only strings no longer appear anywhere in the file
- `.agenda-event-entry` and `.agenda-event-title` added to `calendar.mobile.css`, mirroring `.agenda-quest-entry` / `.agenda-quest-title` surface and geometry exactly, distinguished only by a fixed `#6f42c1` left border (no status variants, since an event has none) and matching `:active` pressed state

## Task Commits

1. **Task 1: Widen the agenda filter, rewrite the empty state, and render event entries first** - `2b67382` (feat)
2. **Task 2: Add the mobile agenda event entry styles** - `d92411f` (feat)
3. **Task 3: Human check — mobile agenda verified under a real mobile User-Agent** - NOT STARTED (blocking checkpoint, awaiting developer)

**Plan metadata:** will be committed once Task 3 is resolved and the plan closes

## Files Created/Modified
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - widened agenda filter, month-neutral empty state, event entry loop before the quest entry loop
- `QuestBoard.Service/wwwroot/css/calendar.mobile.css` - `.agenda-event-entry` and `.agenda-event-title` rules appended after the existing quest entry rules

## Decisions Made
None - Tasks 1 and 2 executed exactly as planned, following the desktop precedent from plan 74-06 (`EventOnDay.TimeLabel`, the `#6f42c1` accent, no per-day cap).

## Deviations from Plan
None - plan executed exactly as written for Tasks 1 and 2.

## Issues Encountered
None for Tasks 1-2. A transient `MSBUILD : error MSB4166: Child node "2" exited prematurely` occurred once during verification when a `dotnet build` and a `dotnet test` were run as concurrent processes against the same output directory; a clean re-run of `dotnet build` alone immediately after confirmed 0 errors, 20 warnings (all pre-existing `AngleSharp`/`HtmlSanitizer` package-version warnings unrelated to this change). Not a code issue — no fix required, no deviation logged.

## User Setup Required
None - no external service configuration required.

## Verification Status (Tasks 1-2)
- `dotnet build` exits 0 (6 projects, 0 errors, 20 pre-existing warnings).
- `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` — 49/49 passed (integration project; no unit-test matches for that filter, which is expected).
- `dotnet test --filter "FullyQualifiedName~MobileCssTests"` — 4/4 passed.
- `dotnet test --filter "FullyQualifiedName~EventCalendarPartialTests"` — 3/3 passed (structural-protection tests from plan 74-06 remain green).
- Full suite: 313 unit + 446 integration = 759/759 passed, 0 failures — baseline preserved.
- `git status --porcelain QuestBoard.Service/Views/Shared/_Calendar.cshtml QuestBoard.Service/Views/Quest/` — clean, no modification (Task 1 acceptance criterion).
- All grep-based acceptance criteria for Tasks 1 and 2 (filter expression, empty-state strings, class names, colour values, line ordering) verified and passing.

## CHECKPOINT REACHED

**Type:** human-verify
**Plan:** 74-07
**Progress:** 2/3 tasks complete

### Completed Tasks

| Task | Name | Commit | Files |
| --- | --- | --- | --- |
| 1 | Widen the agenda filter, rewrite the empty state, and render event entries first | `2b67382` | `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` |
| 2 | Add the mobile agenda event entry styles | `d92411f` | `QuestBoard.Service/wwwroot/css/calendar.mobile.css` |

### Current Task

**Task 3:** Human check — mobile agenda verified under a real mobile User-Agent
**Status:** awaiting verification
**Blocked by:** requires a genuine mobile User-Agent check by the developer; devtools viewport emulation alone is explicitly not accepted per the plan's own design (this codebase has a live precedent of mobile markup that was never actually selected by the platform view switch)

### Checkpoint Details

**What was built:** The mobile calendar agenda now lists days that have only events, renders event entries above quest entries within each day, prints the all-day wording when an event has no start time, taps through to the event details view, and shows a month-neutral empty state.

**How to verify:**
1. Run the app (`dotnet run --project QuestBoard.Service`).
2. Open the site on an actual phone on the same network, or in a desktop browser with a genuine iPhone or Android User-Agent string set (Firefox `general.useragent.override`, or Chrome's Network Conditions panel with a custom User-Agent — not the device-toolbar viewport toggle alone).
3. Sign in as a Dungeon Master and go to the Calendar. Confirm you get the agenda list layout, not the desktop grid.
4. Create an event on a day that has NO quest. Confirm that day now appears as its own agenda section.
5. Create a second event, with no start time, on a day that DOES have a quest. Confirm within that day the two event entries appear above the quest entry, and the no-start-time entry shows the all-day wording on the right rather than a blank space.
6. Tap an event entry and confirm it opens the event details view.
7. Navigate to a month with neither quests nor events and confirm the empty state reads "Nothing This Month" with the neutral body text — not a message about quests only.

Suggested viewport for verification: ~375px width (matches the developer's usual mobile check width), with a real mobile User-Agent string set as described above.

### Awaiting

Developer to type "approved", or to describe what is missing, mis-ordered, or unreachable on the real mobile view so Task 1 can be revisited before resuming.

## Next Phase Readiness
- Tasks 1-2 are code-complete, committed, and verified by automated build/test — nothing further to implement pending the checkpoint outcome.
- If the developer approves, Task 3 closes with no further code changes and this plan is done.
- If the developer reports a defect, it must be fixed by revisiting Task 1 (not Task 2 — the CSS mirrors the quest entry exactly and has no reported ambiguity in the plan).

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: pending Task 3 checkpoint*
