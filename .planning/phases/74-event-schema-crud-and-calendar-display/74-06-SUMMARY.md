---
phase: 74-event-schema-crud-and-calendar-display
plan: 06
subsystem: ui
tags: [aspnet-mvc, razor, css, calendar]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 04)
    provides: Event domain model, IEventService.GetEventsForCalendarAsync, EventViewModel TimeLabel wording pattern
affects: [74-07, 74-08, 75-event-availability]
provides:
  - EventOnDay view model (Event, IsAllDay, TimeLabel) mirroring QuestOnDay
  - CalendarViewModel.Events / GetEventsForDate(DateTime) — the single DateOnly-to-DateTime conversion site
  - CalendarDay.EventsOnDay, defaulting to an empty list so unrelated call sites of the shared partial render nothing
  - CalendarController wired to IEventService.GetEventsForCalendarAsync alongside the existing quest fetch
  - Desktop calendar day cell renders an events block above the quest list, purple-accented chip with its own icon, click-through to /Events/Details/{id}
  - Legend card Event row and updated "Click quests or events for details" hint
  - Fixed-height desktop calendar cells with an internal per-day scroll region (.day-cell-items) holding both events and quests, replacing the grow-the-cell approach originally planned

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Structural (not flag-based) protection: CalendarViewModel.Events defaults to an empty list and the render block's only condition is whether the day's collection has items, so the five quest-detail call sites that never populate it inherit a safe default automatically — proven by EventCalendarPartialTests, not code review"
    - "Fixed-height grid cell with one internal scroll region: the day-number stays outside .day-cell-items so it never scrolls out of view, and both the events block and the quest list share a single scrollbar rather than each nesting its own"

key-files:
  created:
    - QuestBoard.Service/ViewModels/CalendarViewModels/EventOnDay.cs
  modified:
    - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.Service/Views/Calendar/Index.cshtml

key-decisions:
  - "Developer-requested deviation at the Task 4 checkpoint: desktop calendar cells must not grow at all, even though the plan's stated approach (D-08 accepted cost) was a growable grid row. Reverted grid-auto-rows to a fixed 120px on all three .calendar-body rules and made the day's items (events + quests) scroll inside the cell instead, via a new .day-cell-items wrapper that keeps the day-number visible and applies at every breakpoint."
  - "Removed the Take(3) caps on both the events and quest loops now that everything is reachable by scrolling, and removed the dead +N more block. This also fixes a pre-existing silent-drop bug: events never had a +N more indicator while quests did, so a 4th event on a day was being dropped with no indication to the user."

patterns-established:
  - "Board-event chip system mirrors the quest chip system (.calendar-event mirrors .quest-event) but is distinguished by an unclaimed accent colour (#6f42c1) and an icon rather than by position alone"

requirements-completed: [EVENT-03]

coverage:
  - id: D1
    description: "An event on a board appears in its day's cell on the desktop calendar, in its own accent colour with its own icon, above the quest list, linking to the event details view"
    requirement: "EVENT-03"
    verification:
      - kind: manual_procedural
        ref: "Developer live-verified at 1280px and 1920px: event chip renders above quests, purple #6f42c1 left border plus fa-calendar-day icon distinguishes it from green quest chips without reading text, chip click opens /Events/Details/{id}"
        status: pass
    human_judgment: true
    rationale: "Perceptual distinctness and silent CSS clipping cannot be proven by a DOM assertion; both required eyes on a real browser, per the plan's own Task 4 checkpoint design."
  - id: D2
    description: "Quest detail pages render zero event markup even when a same-day event exists on the same board"
    requirement: "EVENT-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs#QuestDetails_WithSameDayEventOnSameBoard_RendersNoEventMarkup"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs#QuestDetailsMobile_WithSameDayEventOnSameBoard_RendersNoEventMarkup"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs#QuestCreate_WithExistingEventOnChosenDate_SucceedsUnchanged"
        status: pass
    human_judgment: false
  - id: D3
    description: "Desktop calendar day cells stay a fixed height regardless of how many events and quests a day holds; all items remain reachable through a single internal scrollbar, and the day number never scrolls out of view — a developer-requested deviation from the plan's original grow-the-cell design"
    verification:
      - kind: manual_procedural
        ref: "Developer live-verified: cell height fixed at 120px, day's items scroll inside the cell at both 1280px and 1920px, day number stays visible"
        status: pass
    human_judgment: true
    rationale: "Visual layout behavior (fixed height, scroll affordance, no clipping) requires eyes on a real browser; confirmed by the developer as part of the Task 4 checkpoint resolution."

# Metrics
duration: 35min
completed: 2026-08-26
status: complete
---

# Phase 74 Plan 06: Desktop Calendar Event Rendering Summary

**Events render as purple-accented chips above the quest list in each desktop calendar day cell, with the cell height held fixed and all items reachable through one internal scrollbar per day instead of growing the grid row.**

## Performance

- **Duration:** 35 min (this dispatch — closing out a plan whose Tasks 1-3 were previously implemented and merged)
- **Completed:** 2026-08-26T15:57:25Z
- **Tasks:** 4 (1-3 previously merged; Task 4 checkpoint resolved and closed by this dispatch)
- **Files modified:** 2 (this dispatch's design-change commit); 7 total across the plan

## Accomplishments
- `EventOnDay` view model added, mirroring `QuestOnDay`, with `TimeLabel` wording "All day" for events with no start time
- `CalendarViewModel.Events` defaults to an empty list; the single `DateOnly.FromDateTime` conversion site lives in `GetEventsForDate`
- `CalendarController` fetches events alongside quests via `IEventService.GetEventsForCalendarAsync`
- Desktop calendar day cell renders an events block above the quest list, gated only on whether the day has events — no flag, no `IsDetailsPage`/`currentQuestId` check
- Legend card gained an Event row and its hint now reads "Click quests or events for details"
- Task 4 human-verify checkpoint resolved: the developer confirmed all listed behaviors work correctly in a real browser at 1280px and 1920px
- Developer-requested design change applied and verified: desktop calendar cells now stay a fixed 120px instead of growing, with the day's items (events and quests together) scrolling inside the cell via a new `.day-cell-items` wrapper

## Task Commits

Tasks 1-3 were implemented and merged prior to this dispatch:

1. **Task 1: Add EventOnDay, wire the events collection through the calendar view model and controller** - `ebb20b2` (feat)
2. **Task 2: Render the events block in the shared partial and add its styles** - `15bd74d` (feat)
3. **Task 3: Add the Legend event row and update its hint** - `e953d8b` (feat)

This dispatch:

4. **Task 4: Checkpoint resolution — fixed-height cell with internal scroll (developer-requested deviation)** - `315d388` (fix)

**Plan metadata:** committed alongside this SUMMARY (see final commit below)

## Files Created/Modified

Prior (Tasks 1-3):
- `QuestBoard.Service/ViewModels/CalendarViewModels/EventOnDay.cs` - new per-day event wrapper
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs` - `EventsOnDay` list
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs` - `Events` collection, `GetEventsForDate`
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - `IEventService` wiring
- `QuestBoard.Service/Views/Calendar/Index.cshtml` - Legend Event row and hint

This dispatch (Task 4 checkpoint resolution):
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` - wrapped events + quests in `.day-cell-items`, removed both `Take(3)` caps and the dead "+N more" block
- `QuestBoard.Service/wwwroot/css/calendar.css` - all three `.calendar-body` rules changed from `minmax(120px, auto)` to a fixed `120px`; added `.day-cell-items` scroll container with themed scrollbar styling; removed the now-unused `.more-events` rule

## Decisions Made

**Developer-requested change at the Task 4 checkpoint:** live verification showed the day cell growing from 120px to 152px with 3 events plus 1 quest, which the developer rejected — desktop calendar cells must stay a fixed height. Resolution: reverted `grid-auto-rows` to a fixed `120px` on all three `.calendar-body` rules (base, `.details-page`, `.quest-details-page`), and added a `.day-cell-items` flex child with `overflow-y: auto` wrapping both `.calendar-events` and `.quest-events` so a day's items scroll as one unit inside the fixed-height cell. The day-number header sits outside this wrapper (`flex-shrink: 0`) so it never scrolls out of view. This wrapper is unconditional CSS (not inside a media query), so mobile breakpoints — already fixed height with `overflow: hidden` — pick up the same scroll behavior automatically rather than clipping more content than before.

With scrolling in place, the `Take(3)` caps on both the events and quest loops were removed, and the dead `+N more` block (which only ever counted quests) was removed along with its now-unused `.more-events` CSS rule. This also fixes a pre-existing silent-drop bug: events had no overflow indicator at all while quests did, so a 4th event on a day was silently dropped from the rendered output with no visible sign to the user. Both platforms now render every event and every quest for a day, reachable via scroll.

## Deviations from Plan

### Auto-fixed Issues

**1. [Developer-requested design change, resolved at Task 4 checkpoint] Fixed-height desktop cells with internal scroll instead of growing rows**
- **Found during:** Task 4 (human-verify checkpoint)
- **Issue:** The plan's originally accepted design (D-08 accepted cost) let the desktop calendar grid row grow via `grid-auto-rows: minmax(120px, auto)` so a day with many items would not clip. Live verification showed this growing the cell from 120px to 152px for a day with 3 events and 1 quest, which the developer explicitly does not want — desktop cells must stay a fixed height.
- **Fix:** Changed all three `.calendar-body` `grid-auto-rows` declarations to a fixed `120px`. Added a `.day-cell-items` scroll wrapper around the events and quest blocks so all content stays reachable through one scrollbar per day, with the day number kept outside the wrapper so it stays visible. Removed the `Take(3)` caps on both collections and the dead `+N more` block, since scrolling makes truncation unnecessary.
- **Files modified:** `QuestBoard.Service/wwwroot/css/calendar.css`, `QuestBoard.Service/Views/Shared/_Calendar.cshtml`
- **Verification:** `dotnet build` exits 0; full test suite 759/759 passing including `EventCalendarPartialTests` 3/3; grep confirms zero `Take(3)` remaining in `_Calendar.cshtml` and zero `minmax(120px, auto)` remaining in `calendar.css`, with exactly three fixed `120px` rules present.
- **Committed in:** `315d388`

---

**Total deviations:** 1 developer-requested design change (resolved and verified at the plan's own checkpoint)
**Impact on plan:** The change narrows the plan's accepted D-08 cost (a growable row) into a stricter fixed-height-plus-scroll design at the developer's explicit request. All other plan deliverables (event rendering, colour/icon distinction, Legend row, structural leak protection) are unaffected and remain exactly as planned.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Desktop calendar event rendering is complete and verified end-to-end, including the fixed-height/scroll behavior the developer required.
- Quest detail pages remain untouched and proven event-free by `EventCalendarPartialTests`.
- Ready for the mobile agenda view and any subsequent event-availability phases that build on `CalendarViewModel.Events`.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-26*
