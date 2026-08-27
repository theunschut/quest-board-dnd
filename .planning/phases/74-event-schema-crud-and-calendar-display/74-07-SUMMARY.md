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
  - "None — Tasks 1 and 2 executed exactly as planned. Task 3's human-verify checkpoint has been resolved: the developer confirmed the mobile agenda under a genuine mobile User-Agent, and the plan is now complete."

patterns-established: []

requirements-completed: [EVENT-04]

coverage:
  - id: D1
    description: "A day that has an event but no quest appears in the mobile agenda, which previously listed only days with quests"
    requirement: "EVENT-04"
    verification:
      - kind: manual_procedural
        ref: "Task 3 human-verify checkpoint — developer confirmed under a genuine mobile User-Agent (Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/148.0.0.0 Mobile Safari/537.36) at a 375x812 viewport. A day holding only an event now appears as its own agenda section (verified with 'WEDNESDAY, AUGUST 12 — Solstice Vigil — All day')."
        status: pass
    human_judgment: true
    rationale: "This codebase has a live precedent of mobile markup that was never actually selected by the platform view switch, so devtools viewport emulation alone was not accepted as verification. A real mobile User-Agent check was required and performed: the same URL returned different markup per User-Agent (desktop UA 4569 bytes with zero 'mobile' references; Android UA 3762 bytes with four), confirming MobileDetectionMiddleware and MobileViewLocationExpander actually switched views rather than assuming the switch occurred."
  - id: D2
    description: "Events render before quests within a day section, and an event with no start time always shows the all-day wording rather than a blank time slot"
    requirement: "EVENT-04"
    verification:
      - kind: manual_procedural
        ref: "Task 3 human-verify checkpoint — developer confirmed under the same genuine mobile User-Agent check as D1. Within a day, event entries render above the quest entry (3 events then the quest); an event with no start time renders the all-day wording, not a blank."
        status: pass
    human_judgment: true
    rationale: "Same as D1 — required and received a real mobile User-Agent check, not automatable."
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
status: complete
---

# Phase 74 Plan 07: Mobile Calendar Agenda Event Rendering Summary

**Mobile agenda widened to list days with events (not only quests), with event entries rendered above quest entries reading the same TimeLabel property as the desktop chip, and a month-neutral empty state — all 3 tasks complete and committed, Task 3 human-verify checkpoint confirmed under a genuine mobile User-Agent.**

## Performance

- **Duration:** 25 min (Tasks 1-2 implementation) plus Task 3 human verification
- **Started:** 2026-08-27T00:00:00Z (approx)
- **Tasks:** 3/3 complete
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
3. **Task 3: Human check — mobile agenda verified under a real mobile User-Agent** - APPROVED (developer confirmed under a genuine Android/Chrome mobile User-Agent at 375x812)

**Plan metadata:** committed with this summary — plan closed.

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

## Verification Status (Task 3 — Human Check)

Task 3 was performed and passed. Method: driven in a real browser under a genuine device
User-Agent (`Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/148.0.0.0
Mobile Safari/537.36`) at a 375x812 viewport. The User-Agent requirement was confirmed
satisfied, not assumed: the same URL returned different markup per User-Agent (desktop UA
4569 bytes with zero "mobile" references; Android UA 3762 bytes with four), proving
MobileDetectionMiddleware and MobileViewLocationExpander actually switched views. Devtools
viewport emulation alone was not relied on.

Results, all passing:
- Mobile agenda list rendered, not the desktop grid (no `.calendar-grid` in the DOM)
- A day holding only an event now appears as its own agenda section ("WEDNESDAY, AUGUST 12 —
  Solstice Vigil — All day"), which is the behaviour this plan exists to add
- Within a day, event entries render above the quest entry (3 events then the quest)
- An event with no start time renders the all-day wording, not a blank
- Tapping an event entry navigates to /Events/Details/{id} and renders the event details view
- Events are distinguishable from quests without reading text: purple left accent plus a
  calendar icon, versus the quest's green accent and no icon
- Empty state on a month with neither quests nor events reads "Nothing This Month — No quests
  or events are planned for {Month} {Year}. Check another month." — month-neutral, mentions
  both quests and events

## Follow-Up Candidates (pre-existing, out of scope for this plan)

Two observations recorded during Task 3 verification. Both are pre-existing patterns this
plan correctly mirrored rather than defects it introduced:

1. Agenda entries are `<div onclick="window.location.href=...">` rather than anchors, so they
   are not keyboard-focusable and are not announced as links. The pre-existing
   `.agenda-quest-entry` uses the identical pattern; fixing it properly means changing quest
   entries too, which is a separate accessibility pass.
2. Agenda entry tap targets measure 40px tall, under the 44px iOS / 48px Android guidance —
   again identical to the pre-existing quest entries.

## CHECKPOINT RESOLVED

**Type:** human-verify
**Plan:** 74-07
**Progress:** 3/3 tasks complete

### Completed Tasks

| Task | Name | Commit | Files |
| --- | --- | --- | --- |
| 1 | Widen the agenda filter, rewrite the empty state, and render event entries first | `2b67382` | `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` |
| 2 | Add the mobile agenda event entry styles | `d92411f` | `QuestBoard.Service/wwwroot/css/calendar.mobile.css` |
| 3 | Human check — mobile agenda verified under a real mobile User-Agent | (verification-only, no code commit) | n/a |

### Checkpoint Details

**What was built:** The mobile calendar agenda now lists days that have only events, renders event entries above quest entries within each day, prints the all-day wording when an event has no start time, taps through to the event details view, and shows a month-neutral empty state.

**How it was verified:** Driven in a real browser under a genuine Android/Chrome mobile
User-Agent at a 375x812 viewport, with the User-Agent switch itself confirmed via differing
response byte counts and markup between desktop and mobile UAs (see "Verification Status
(Task 3 — Human Check)" above). All seven verification steps from the original checkpoint
passed.

### Resolution

Developer approved. No defects found; the two pre-existing accessibility observations above
are logged as follow-up candidates, not blockers for this plan.

## Next Phase Readiness
- All 3 tasks are code-complete (Tasks 1-2) or verified (Task 3), committed, and confirmed by both automated build/test and a real mobile User-Agent check — this plan is done.
- The two pre-existing accessibility observations (non-focusable agenda entries, sub-guideline tap target height) are candidates for a future accessibility pass covering both quest and event agenda entries together — not required by this plan.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-27*
