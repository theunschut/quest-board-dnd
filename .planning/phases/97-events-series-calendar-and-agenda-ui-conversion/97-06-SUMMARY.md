---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 06
subsystem: events-calendar-dates
tags: [wall-clock, html-helper, culture, rendered-text, guard]
requires: ["97-04"]
provides:
  - "ServerOnlyWallClockFormats: time-24h, weekday-short, weekday-long, weekday-long-year (server-only, current-culture text, invariant datetime)"
  - "All 18 direct date ToString calls in the area's views rendered through Html.WallClock with unchanged visible text"
  - "QuestOnDay.TimeLabel for the calendar quest title attribute"
  - "EventsConversionGuardTests: the area guard later slices extend (raw dates, planning references, scanner self-test)"
affects: [97-07]
key-files:
  created:
    - QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
  modified:
    - QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/QuestOnDay.cs
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml
    - QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Events/Details.cshtml
    - QuestBoard.Service/Views/Agenda/Index.cshtml
    - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Series/Details.cshtml
    - QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs
    - .claude/ui-guidelines.md
decisions:
  - "New styles format their visible text with CultureInfo.CurrentCulture (the culture the replaced calls used); the four existing styles stay invariant; datetime stays invariant ISO for every style"
  - "The phone day label keeps its text-uppercase class and renders mixed case through the helper; tests assert text and class, not an upper-cased string"
metrics:
  completed: 2026-11-12
status: complete
actuals:
  tokens: 12000
  tasks: 3
  commits: 1
---

# Phase 97 Plan 06: Date expressions moved to the wall-clock helper Summary

Every date the calendar, agenda, events and series views formatted themselves now renders through `Html.WallClock`, with
the same visible text on every line (18 of 18 equal to the frozen baseline), no layout change and no change to `site.js`.
The helper gained four server-only styles in a separate table; the area guard every later slice extends is in place.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: four styles, unit test, Events/Details line, rendered-text fact, area guard | done | 8228e3e6 |
| 2 Calendar family: `_Calendar`, `_Calendar.Mobile`, phone Calendar page, `QuestOnDay.TimeLabel` | done | 8228e3e6 |
| 3 Events, Agenda, Series lines; guideline sentence; guard widened to all 17 files; baseline comparison | done | 8228e3e6 |

All three tasks are one commit (see Deviations). The tracer gate ran as a green targeted run (helper tests plus the new
guard and render facts) before the wider suites.

## The 18 lines

| # | File:line (at planning) | Old expression | New style |
|---|---|---|---|
| 1 | Calendar/Index.Mobile:98 | `day.Date.ToString("dddd, MMMM d").ToUpper()` | `weekday-long` (`.ToUpper()` dropped; element keeps `text-uppercase`) |
| 2 | Calendar/Index.Mobile:121 | `ToString("HH:mm")` | `time-24h` |
| 3 | Agenda/Index:107 | `row.Date.ToString("ddd, MMM d")` (DateOnly) | `weekday-short` |
| 4 | Agenda/Index:110 | `StartTime.Value.ToString("HH:mm")` (TimeOnly) | `time-24h` |
| 5 | Agenda/Index.Mobile:108 | as 3 | `weekday-short` |
| 6 | Agenda/Index.Mobile:111 | as 4 | `time-24h` |
| 7 | Shared/_Calendar:78 | `ToString("HH:mm")` inside `title="..."` | `@questOnDay.TimeLabel` (new `QuestOnDay.TimeLabel`) |
| 8 | Shared/_Calendar:86 | `ToString("HH:mm")` | `time-24h` |
| 9 | Shared/_Calendar.Mobile:26 | `ToString("dddd, MMMM d")` | `weekday-long` |
| 10 | Shared/_Calendar.Mobile:27 | `ToString("HH:mm")` after "at" | `time-24h` |
| 11 | Series/Details:40 | `AnchorDate.ToString("dddd, MMMM d, yyyy")` | `weekday-long-year` |
| 12 | Series/Details:64 | `EndDate.Value.ToString(...)` | `weekday-long-year` |
| 13 | Series/Details:116 | `occurrence.Date.ToString(...)` | `weekday-long-year` (keeps ` &middot; TimeLabel`) |
| 14 | Events/Details:48 | `Model.Date.ToString("dddd, MMMM d, yyyy")` | `weekday-long-year` |
| 15 | Events/Index:66 | `ToString("ddd, MMM d")` | `weekday-short` |
| 16 | Events/Index:69 | `ToString("HH:mm")` | `time-24h` |
| 17 | Events/Index.Mobile:80 | as 15 | `weekday-short` |
| 18 | Events/Index.Mobile:83 | as 16 | `time-24h` |

`DateOnly` and `TimeOnly` values convert at the call site (`date.ToDateTime(TimeOnly.MinValue)`, `date.ToDateTime(time)`);
no helper overload was added. Left alone as planned: `Events/Edit` machine value, the boolean `ToString().ToLower()`, the
dialog interpolations in `Events/Details`, `Model.TodayLabel`, and the view models' existing `TimeLabel` properties.

## Culture decision and nl-NL results

The four new styles format visible text with `CultureInfo.CurrentCulture`, the culture the replaced `.ToString(format)`
calls used; `datetime` stays invariant `yyyy-MM-ddTHH:mm` for every style; the four existing styles keep invariant text
(pinned unchanged). The app sets no request culture, so current culture is the host culture exactly as before.

- Host culture for the suites and for the 8300 app: `LANG=en_GB.UTF-8`.
- `WallClockAreaStylesTests` (17 rows incl. theories): exact text per style, nl-NL equals the old format applied in nl-NL
  with the datetime attribute still invariant, the four existing styles still invariant under nl-NL, `BuildLocalTime`
  still rejects the new styles, unknown style still throws.
- `EventsDateRenderTests` (18 rows): each moved line's old text computed from the same seeded value, under the host
  culture and under nl-NL (four culture slots swapped, restored in `finally`, non-parallel collection). Old and new text
  are equal on all rows; nl-NL rows also assert the text is not the English text, so the swap provably reaches the server.
  Mutation check: switching the new styles to the invariant culture failed 8 of the nl-NL rows, then reverted.
- Production `LANG` therefore no longer affects correctness (informational for the later release sheet).

## Verification

- `dotnet test QuestBoard.UnitTests`: **1412 passed, 0 failed, 0 skipped** (1395 after 97-04, +17).
- `dotnet test QuestBoard.IntegrationTests` (with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and the user-secrets connection supplied as the migrator test variable): **1885 passed, 0 failed, 0 skipped** (1864 after 97-04, +21; 3 m 23 s).
- Test inventory against `tests-before.txt`: no row missing except two whose names break across a line on edge whitespace (a known artifact of splitting the inventory, also noted in 97-04); 48 rows new since the phase start.
- date-lines on 8300 (`after/97-06/date-lines.json`) against `before/date-lines.json` from the frozen baseline: **18 of 18 keys equal, 117 visible texts equal** (innerText after CSS, so the upper-cased phone day label compares as SUNDAY, MAY 2 on both sides; the title attribute line compares its attribute text).
- Unchanged from the plan start: `WallClockMarkupTests.cs`, `LocalTimeMarkupTests.cs`, `WallClockUnmovedTests.cs`, `SiteJsHydrationTests.cs`, `site.js` (empty diff). Feed files: empty diff. `HtmlHelperExtensions` diff only adds the new table, its comment and the lookup change inside `BuildWallClock`.
- Direct date `ToString` calls left in the area's views: 0. `ui-guidelines.md` has `weekday-long-year` once and keeps `TIMESTAMP_FORMATS`.
- site.js hydration was read, not changed: a wall-clock element whose `data-style` has no `TIMESTAMP_FORMATS` entry is left as the server rendered it.

## Deviations from Plan

**1. [Rule 1 - Bug] Existing linked-board phone agenda test pinned the server-side upper-casing**
- **Found during:** full integration run
- **Issue:** `LinkedBoardMobileCalendarTests.DaySection` found a day section by the upper-cased label text; with the label now rendered mixed case (capitals come from the `text-uppercase` class, visible text unchanged) 8 facts failed.
- **Fix:** match the label with `StringComparison.OrdinalIgnoreCase` and say why in the comment; no assertion weakened beyond case.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs`
- **Commit:** 8228e3e6

**2. One commit for all three tasks.** The area guard is scoped to every file once the lines move, so splitting would have left red intermediate commits (same reasoning as 97-04). The tracer gate was run as a green targeted run before widening.

**3. Tests-first order.** The unit and render tests were written before the production edit, but the views were edited in one scripted pass and I did not run a red state; the guard and facts were instead proven to bite by a mutation (new styles forced to invariant culture fail 8 nl-NL facts).

**4. Ports:** 8000/8001 read as 8300/8301 per the orchestrator overrides; 8301 was not touched.

**5. Files beyond the plan's list:** `LinkedBoardMobileCalendarTests.cs` (deviation 1). `PhaseSourceFiles` also lists the two 97-04 files it touched (`CalendarController.cs`, `CalendarBoardClockTests.cs`) so the planning-reference scan covers them.

## Known Stubs

None.

## Threat Flags

None. T-97-16 and T-97-17 mitigated as planned (no `Html.LocalTime`, no `site.js` change, baseline comparison equal); T-97-19: no feed file or Events/Quests write touched.

## Self-Check: PASSED

Three new test files exist; commit 8228e3e6 exists on feature/phase-97; both full suites green; date-lines 18 of 18 equal.
