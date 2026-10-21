---
phase: 93-per-board-page-selection-and-home-page
plan: 08
subsystem: ui
tags: [aspnet-mvc, razor, board-pages, calendar, events]

requires:
  - phase: 93-per-board-page-selection-and-home-page
    provides: BoardPage gate attributes, IBoardPageResolver, BoardPageAvailabilityExtensions.Serves, BoardPages view injection
provides:
  - Calendar that never loads a switched-off board's events or series runway
  - Event and series saves that land on the Events list when Calendar is off
  - Board Availability button and event form Cancel links that follow the page switches
affects: [93-10, 93-11, 93-12]

actuals:
  tokens: 9000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Not loaded rather than hidden: a switched-off page's data is never read, so no view can leak a link"
    - "Redirect-after-save helpers pick the landing page from the board's page availability"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Controllers/Events/SeriesController.cs
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Events/Create.cshtml
    - QuestBoard.Service/Views/Events/Edit.cshtml

key-decisions:
  - "Events are not loaded (rather than hidden) with Events off, so the calendar carries no event or series link on either layout"
  - "Fallback landing for every event/series redirect and Cancel link when Calendar is off is /Events"
  - "Calendar on with Events off on a Campaign board renders an empty calendar with 200; no rule ties Calendar to Events"

requirements-completed: [D-07, D-08, D-09]

duration: 20min
completed: 2026-10-21
status: complete
plan_head_before: 7c452094929a9d1e64472f83d498955cba48d94e
commits: 4

coverage:
  - id: D1
    description: "With Events off the Calendar loads none of the board's events and no series runway banner, on both layouts; events reappear when Events is switched back on"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#Calendar_EventsOff_DoesNotLoadTheBoardsEvents"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#Calendar_EventsOff_ShowsNoSeriesRunwayBanner"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#Calendar_EventsSwitchedBackOn_ShowsTheEarlierEventAgain"
        status: pass
    human_judgment: false
  - id: D2
    description: "The Calendar's Board Availability button is hidden with Events off on both layouts"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#Calendar_EventsOff_HidesTheBoardAvailabilityButton"
        status: pass
    human_judgment: false
  - id: D3
    description: "With Calendar off, event create/edit/delete, series delete/detach and the form Cancel links land on /Events; with Calendar on they are unchanged"
    requirement: "D-08"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#EventCreate_CalendarOff_RedirectsToTheEventsList"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#SeriesDeleteAndDetach_CalendarOff_RedirectToTheEventsList"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#EventCreateForm_CalendarOff_CancelLinksToTheEventsList"
        status: pass
    human_judgment: false
  - id: D4
    description: "Campaign board with Events off and Calendar on renders an empty calendar with 200"
    requirement: "D-09"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs#Calendar_CampaignBoardWithEventsOff_RendersAnEmptyCalendar"
        status: pass
    human_judgment: false
---

# Phase 93 Plan 08: Events and Calendar in-page entry points Summary

**Calendar skips a switched-off board's events and series runway, and event/series saves and form Cancel links fall back to the Events list when the calendar is off.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-10-21T12:46:33Z
- **Tasks:** 2 (both TDD)
- **Files modified:** 8 (1 created)

## Accomplishments
- `CalendarController` reads the board's page availability and, with Events off, never loads events or the low-runway series, so no event or series link can render on either layout. The rows stay in the database and return when Events is switched on.
- `EventsController.RedirectAfterSaveAsync` replaces `RedirectToCalendarMonth`: event create (one-off and recurring), edit and delete land on the Calendar month as before, or on `/Events` when Calendar is off. `SeriesController` delete and detach follow the same rule.
- The Calendar's Board Availability button is gated on Events on both the desktop and mobile twins; the Create and Edit Cancel links point at `/Events` when Calendar is off.
- `BoardPageEventsCalendarTests` (26 cases) covers both user agents, the Campaign board, the D-09 round trip, and the unchanged Calendar-on behaviour.

## Task Commits

1. **Task 1 RED** - `8d22306f` (test)
2. **Task 1 GREEN** - `0b7f706c` (feat)
3. **Task 2 RED** - `8351f21d` (test)
4. **Task 2 GREEN** - `ce0d4fa2` (feat)

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - includeEvents via `Serves(BoardPage.Events)`; events and runway not loaded when off
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - `RedirectAfterSaveAsync`
- `QuestBoard.Service/Controllers/Events/SeriesController.cs` - `RedirectAfterRemovalAsync` for delete and detach
- `QuestBoard.Service/Views/Calendar/Index.cshtml`, `Index.Mobile.cshtml` - Board Availability button gated on Events (linked block untouched)
- `QuestBoard.Service/Views/Events/Create.cshtml`, `Edit.cshtml` - Cancel link follows Calendar switch
- `QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs` - proofs

## Decisions Made
None beyond the plan's recorded choices; the series redirect helper is a private method named `RedirectAfterRemovalAsync` (the plan only specified the behaviour).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Full `dotnet test` shows one failure, `MigratorBackupAndApplyCliTests...WhenServerIsUnreachable...(backup)`, the known intermittent pre-existing failure already logged in deferred-items.md. All integration tests pass (1435 passed, 11 skipped).

## TDD Gate Compliance
RED (`test(93-08)`) commits precede GREEN (`feat(93-08)`) commits for both tasks. Each RED run failed on the intended assertions (11 of 18, then 4 of 26) while the Events-on and Calendar-on cases already passed.

## Known Stubs
None.

## Threat Flags
None. Redirect targets are fixed route names (T-93-33); no request data is used.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
Plan 10 can add its Quests condition beside `includeEvents` in `CalendarController`.

## Self-Check: PASSED
- Created file `QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs` exists; commits 8d22306f, 0b7f706c, 8351f21d, ce0d4fa2 exist on `worktree-agent-p93-08-1792585981`.
- `git rev-list --count 7c452094..HEAD` measured 4 before the SUMMARY commit.
- No planning references in added source lines.

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
