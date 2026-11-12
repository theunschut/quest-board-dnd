---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 04
subsystem: calendar
tags: [board-clock, calendar, aria-current, architecture-guard, tracer]
requires: ["97-02"]
provides:
  - "ViewBag.BoardToday from IBoardClock on CalendarController.Index"
  - "_Calendar today cell and phone agenda today section follow the board's date, with aria-current=\"date\""
  - "AmbientClockSeamTests closed list extended with the calendar views and partials"
affects: [97-05]
key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
decisions:
  - "aria-current is written as an attribute whose value is null off today, so Razor omits it elsewhere"
metrics:
  completed: 2026-11-12
status: complete
actuals:
  tokens: 5000
  tasks: 2
  commits: 1
---

# Phase 97 Plan 04: Calendar today from the board clock Summary

The calendar's today highlight now follows the board clock instead of the host clock: `CalendarController.Index` hands
`boardClock.Today` to the views as `ViewBag.BoardToday`, `_Calendar.cshtml` compares each day against it, and the phone
agenda marks the board's today section, both with `aria-current="date"`. This is the first production change of the phase
(the tracer): behaviour only, no class, layout or copy change.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Desktop month: controller value, partial comparison, behaviour fact, guard entries | done | bf1865db |
| 2 Phone agenda today section, guard token, fact, sweep | done | bf1865db |

Both tasks landed in one commit because the guard list, the two facts and both views are mutually dependent (the guard
requires the phone view to consume the token); splitting would have left an intermediate red commit.

## Failing then passing

`CalendarBoardClockTests` (clock pinned at 2026-09-05T23:30Z, board zone Europe/Amsterdam, `/Calendar?year=2026&month=9`)
was run first against the unchanged code and both facts failed:

- `DesktopMonth_MarksTheBoardsTodayNotTheHostsToday`: day-6 cell lacked `today` (host clock marks nothing in that month).
- `PhoneAgenda_MarksTheBoardsTodaySection`: no agenda section carried `aria-current`.

After the change both pass; the day-5 cell and the September 5 section carry neither marker.

## Guard entries appended

`GuardedRelativePaths` +4: `Views/Calendar/Index.cshtml`, `Views/Calendar/Index.Mobile.cshtml`, `Views/Shared/_Calendar.cshtml`,
`Views/Shared/_Calendar.Mobile.cshtml`. `RoutedTodayViewTokens` +2: `_Calendar.cshtml` and `Calendar/Index.Mobile.cshtml`, token
`ViewBag.BoardToday`. No line removed from the file (diff has 0 removed lines). Test listing: UnitTests 1378 -> 1384
distinct (+6 guard rows), IntegrationTests +2, nothing removed (two listing rows differ only by edge-whitespace encoding
in my split of the inventory file; the tests exist).

## Verification

- `dotnet test QuestBoard.UnitTests` (with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and the user-secrets connection): **1395 passed, 0 failed, 0 skipped**.
- `dotnet test QuestBoard.IntegrationTests` (same env): **1864 passed, 0 failed, 0 skipped** (3 m 15 s).
- Feed files (`CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`): diff against the phase start is empty.
- Sweep of `cal-current, cal-crowded, calm-current, calm-crowded, qd-open, qd-voting` on 8300 into `~/qb-ui-review/phase-97/after/97-04`: 40 records, 0 not 200. `readability-compare.py` against BEFORE: 36 compared, 0 failing rows, no problems, 0 poster-glyph violations.
- No planning reference in any added line.

## Deviations from Plan

- **Ports:** 8000/8001 read as 8300/8301 per the orchestrator overrides. The 8300 app was restarted for the sweep and then stopped; 8301 was not touched.
- **One commit for both tasks** (see Tasks).
- Plan names `Views/Calendar/Index.cshtml` etc.; `_Calendar.Mobile.cshtml` lives in `Views/Shared` and does not read a clock, so it is guarded only.

## Known Stubs

None.

## Self-Check: PASSED

CalendarBoardClockTests.cs exists; commit bf1865db exists on feature/phase-97; DateTime.Today count in `_Calendar.cshtml` is 0.
