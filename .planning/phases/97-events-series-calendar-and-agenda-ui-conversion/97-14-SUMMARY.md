---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 14
subsystem: calendar-desktop-month
tags: [razor, css-layers, compact-month, guard-tests, quest-details, accessible-names]
requires:
  - phase: 97-03
    provides: the approved compact month drawing and its measured fit
  - phase: 97-05
    provides: Gate 1 answers and the approved version 2 drawing (no header buttons)
  - phase: 97-12
    provides: ViewBag.CurrentUserId on the Calendar page; the page-region test helper pattern
  - phase: 97-13
    provides: calendar.css in the pages layer, scoped .calendar-page and .details-page halves
provides:
  - "Desktop month server-rendered as the approved compact rows: ul.cal-entries per day, one li per entry with marker, time, title, key flag and a hidden detail block"
  - "Two-row page header (title and month navigation, then legend strip plus What do these mean? popover), no Board Availability or My Agenda button"
  - "calendar.css compact month rules (--cal-slots by height, weeks-4|5|6 rows, markers, entries, narrow tiles)"
  - "CalendarMonthMarkupTests, DesktopPageRegions, rewritten linked-board guards and button-placement facts"
affects: [97-15]
actuals:
  tokens: 35000
  tasks: 2
  commits: 3
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarMonthMarkupTests.cs
    - QuestBoard.IntegrationTests/Helpers/DesktopPageRegions.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml
    - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarStyleTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs
    - QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs
    - QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs
key-decisions:
  - "The per-viewer vote reaches the month through ViewBag.CurrentUserId (97-12) and the votes the calendar query already loads, so no extra query per cell; with trailing markers key-only it appears in the hidden accessible name and the detail block, never as a row marker."
  - "An empty month drops the slot minimum (calendar-card--empty) because the one-line note would otherwise push a six-week month 28px past a 1280x720 window."
  - "A called-off linked chip on Quest Details uses muted ink on quieter paper instead of opacity 0.55, the only way the Quest Details sweep reaches zero content contrast failures."
requirements-completed: [D-03, D-04, D-05, D-06, D-07, D-10, D-11, D-14, D-15, D-17, DoC-1, DoC-4, DoC-5, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 14: Desktop compact month, header, legend Summary

The Calendar month is now the approved version 2 drawing, server-rendered and usable without JavaScript: each day lists its events, then quests, then linked entries as 20px rows with the full accessible name and a hidden detail block, linked entries of boards the viewer is not on stay inert, the page header is two rows with a legend strip and popover and no Board Availability or My Agenda button, and Quest Details keeps its vote layout.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: compact month in `_Calendar` and `_LinkedDayEntries`, rules, rewritten guards (also the page header, because the new rules remove the sidebar rules the old header used) | done | 34895d0c |
| 2 Sweep of every calendar and Quest Details state; empty-month fit, Quest Details contrast, facts | done | f0c107c9 |

Tracer gate: auto mode was not active, so the plan's interactive stop would have applied here; the orchestrator asked for a full run, so the slice was verified before expansion (the calendar, linked-board, guard and Quest Details suites, 886 facts, green on the tracer commit) and Task 2 followed.

## Gate 1 answers applied

| Item | Applied as |
|---|---|
| truncation `time` | row is marker, time, title (all-day events have no time column; the word stays in the name and detail) |
| text size 14 | every row, legend and weekday label is 14px or larger |
| today `parchment` | 2px inset parchment ring, filled day-number pill, hidden `Today.` before the date text, `aria-current="date"` from `ViewBag.BoardToday` |
| legend `strip-plus-popover` | `ul.calendar-legend` named Legend plus a `What do these mean?` Bootstrap dropdown (`data-bs-auto-close="outside"`) holding Linked boards, the board names, the dashed sentence and `Click quests or events for details` verbatim |
| runway alert `scroll` | the alert sits above the weekday row, the grid keeps its minimum cell height and the page scrolls by the alert (see below) |
| narrow tiles | below 768px entries are 24px marker-only tiles, weekday names shorten to three letters (full name kept for assistive technology) |
| trailing markers `key-only` | the row carries only the no-building-key marker (and the link glyph on linked rows); the viewer's answer is in the hidden name and the detail block |
| click wording `verbatim` | unchanged |
| fit gap 2px, bottom padding 0 | `--cal-row-gap: 2px`, `padding-block: var(--qb-space-sm) 0` on the page block |
| removeHeaderButtons | no `Board Availability` or `My Agenda` in the Calendar page; both still in the desktop navbar |

## Fit results without the module

Geometry measured with `tools/fit-measure-97-14.mjs` on the working tree (six-week May 2027, `p97player`), and the sweep's `pageScrolls` (corrected for the footer's 32px step; the probe now subtracts the footer's top margin):

| Size | Slots | Cell height | Card bottom vs window | Header |
|---|---|---|---|---|
| 1280x720 | 3 | 90.2 (needs 90) | 720 = 720, fits | 73 |
| 1366x768 | 3 | 98.2 | fits | 73 |
| 1440x900 | 4 | 120.2 | fits | 73 |
| 1536x864 | 4 | 114.2 | fits | 73 |
| 1920x1080 | 5 | 150.2 | fits | 73 |
| 1280x1024 | 5 | 140.8 | fits | 73 |
| 1280x700 | 2 | 86.8 | fits | 73 |
| 1100x800 | 3 | 99.3 | fits | 98 |
| **992x720** | 3 | 90 | **overshoots by 24px** | 98 |

The 992x720 overshoot is the known, recorded one (legend wraps to two lines); it fits from 1100x800. Sweep result across `cal-crowded`, `cal-sixweek`, `cal-fiveweek`, `cal-empty`, `cal-onerow` at the seven fit sizes: no page scroll in any. Without the module four to two cells per month scroll their own list (the no-script fallback), as designed. No horizontal overflow at 320 on any calendar row.

**Header facts (replace the plan's one-row fact):** row 1 (title plus month navigation) 45px (at most 48); whole header 73px at 1280 and wider, 98px at 992 to 1199 for a board with linked boards; two rows at every width from 992px; narrow headers (under 992px) 168.6px at 991 and 768, 227.2px at 320. Calendar `h1` resolves to `--qb-fg` at 8.05:1 over its band; Events/Index `h1` stays gold at 6.22:1.

**Runway alert outcome (Gate 1 item 6, `cal-dm`, `cal-dm-alert`):** the page scrolls by the alert's own height: 73px at 1280x720 with the one-line alert (`cal-dm-alert`), 97px with the two-line alert of the Audit Board's DM (`cal-dm`), 25 to 93px at the other fit sizes (the spare height above the minimum absorbs part of it at tall windows). Nothing clips.

Entries between 768 and 991px wrap to two or three lines (no single ellipsised line there), as drawn.

## Quest Details style comparison

`tools/calendar-style-compare.mjs` against the frozen baseline (8301), `qd-open, qd-voting, qd-update, qd-spectator, qd-finalized`, desktop 1440, 768, 600, 320 and the phone, rest and hover states: the weekday header remains the allowed difference (97-13). Besides it the only computed-style differences are:

- the called-off linked chip: colour, background and opacity (see Deviation 5), nothing else;
- 64 added elements per row set (hidden words beside the key warning, finalized check and viewer's vote) and the decorative icons now `aria-hidden`;
- the phone also-that-day link and its 44px entry box, which 97-12 changed.

The vote classes, inputs, states and order are untouched; `quest-vote.css` untouched. Quest Details rows sweep with **zero** in-main contrast failures (BEFORE: the linked-entry titles at 2.87 to 3.10:1 on four records).

## Sweep and readability results

`readability-compare` against BEFORE for `cal-onerow, cal-crowded, cal-sixweek, cal-fiveweek, cal-empty, cal-current, cal-dm, cal-dm-alert, cal-nojs, ev-index, qd-*` with `--zero-contrast` on every row, compared per User-Agent (the desktop allow file and the phone allow file differ in the renamed link keys):

- desktop: 0 failing rows, 0 residual in-main contrast failures (the only reported items are the phone captures I filtered out);
- phone: no new problem from this plan; the one reported item is the series link inside the runway alert (22px inline link, `cal-dm` at 390 and 412), the same alert sentence 97-12 recorded as unchanged.
- `ev-index`: one link, `Candlemas Feast at the Hollow`, is absent from AFTER because the `Tallow Day Gathering` fixture event (created by the `refreshToday` fixture step on the Audit Board, through the app form) pushed it off the list; not a code change (Events/Index untouched).

Allow files list: the two header buttons removed, the `main` container renamed with the title, the two month buttons named (they had no name before) and every entry link renamed from "Title TIME" to the new full name ("TIME Title, Status, TIME, DM ...").

## Guards rewritten

- `LinkedBoardCalendarTenantIsolationTests`: `DayCell` finds the cell by its day-number element, whatever its tag, and splits on the cell's own class (never `calendar-day__top`); `LinkedBlock` starts at the first linked row; the chip fact that read the old `Board · DM · time` line now reads the structured detail block (`<dd>` board, DM and time); the legend helper reads the strip and popover; `NotContain("linked-day-entries")` became `NotContain("linked-entry")` (the wrapper no longer exists on the month, so the old string would pass vacuously). 31 facts before and after.
- `LinkedBoardStaticGuardTests`: the single-reference fact became "after the own entries in every branch" (two references: one ends the Quest Details cell after the answer markup, one ends the month's list after the own quest rows); banned list, span guard and everything else unchanged. 9 facts before and after.
- `LinkedBoardCalendarStyleTests`: month facts (dashed 1px outline on `.calendar-page .linked-entry`, `.calendar-page .cal-entry-detail` hidden at rest), Quest Details facts (dashed left edge, detail hidden at rest, revealed by hover and focus), strike on the cancelled title on the month and Quest Details, plus a new fact for the called-off chip's ink. 8 facts before, 9 after.
- Facts that read the old month markup were moved to the new one: `CalendarBoardClockTests` (day cell split), `CalendarControllerIntegrationTests` (event row before quest row, legend marker), `EventsDateRenderTests` (the time in `cal-entry__time`), `LinkedBoardEndToEndTests`, `LinkedBoardDeepLinkTests`.
- `CalendarButtonStyleTests` and `BoardPageEventsCalendarTests` desktop facts became "absent from the Calendar page, still linked from the desktop navbar for the right roles", cut by the new `DesktopPageRegions` helper (page `main` versus the navigation `header`): Board Availability for a Dungeon Master with Events on, My Agenda for every member, neither inside the page; nothing deleted.
- `EventsConversionGuardTests`: `CalendarTitle_StaysParchment`, `CalendarHeader_UsesTheCompactBandPadding`, the sheet-scope fact accepts the page root `.calendar-page`, the three views and the new test file join the guard lists.

New: `CalendarMonthMarkupTests` (12 facts, 14 cases with the week-count theory rows): order, accessible names, inert and member linked entries, week classes, today from the board clock, Quest Details layout, header, empty note, popover strings.

## Deviations from Plan

**1. [Plan scope] Task 1 carried the page header too.** The compact rules replace the old sidebar legend and button rules, so `Calendar/Index.cshtml` had to change in the same commit for the suites to stay green; Task 2 then holds the sweep-driven fixes. Commits 34895d0c, f0c107c9.

**2. [Gate 1 / brief] No one-row header fact.** The plan's "one-row header at most 48px" fact and its `cal-onerow` assertion are replaced by the measured facts above; Board Availability and My Agenda are absent from the page and tested as such.

**3. [Rule 1 - Bug] Empty six-week month overshot by 28px at 1280x720** (a quiet board's November): the note plus the slot minimum. An empty month now sets `calendar-card--empty`, which drops the slot minimum so the rows share the height; fits at 1280x720 (card bottom 720). Commit f0c107c9.

**4. [Rule 2 - contrast] Hidden words in the Quest Details icons** (key warning, finalized check, viewer's vote) inherited the light text colour and the probe scored them 1.03 to 1.26:1 against the pale chip; they take `--qb-ink` there. Commit f0c107c9.

**5. [Rule 1 - Bug] Called-off linked chip on Quest Details.** `opacity: 0.55` left its title and hidden text at 3.10:1 (2.87:1 before). It now takes `--qb-ink-muted` on `--qb-surface-paper-alt`, still struck through. This is a visible change inside the Quest Details calendar (a slightly quieter paper instead of a faded chip) and the only intended one besides the weekday header; the zero-contrast bar left no other way. Commit f0c107c9.

**6. Detail blocks follow the data rules.** Own entries carry the date as text inside `<time datetime>` with no `data-` attribute (the shared wall-clock helper writes `data-style`); linked entries carry no date row at all (title, board, DM, time and status only, as the plan lists), because the linked guards and the tenant facts forbid any `data-` attribute in that block. The compact own rows no longer carry `data-quest-id` or `data-proposed-date-id`; the detail page and scripts do not read them from the month (the sweep tooling now reads the id from the link).

**7. Linked partial in two branches.** `_LinkedDayEntries` renders the compact rows when `IsDetailsPage` is not true and keeps today's chips (and wrapper) otherwise. The partial must stay free of the word "vote", buttons, labels, ids and any `data-` attribute (guard); the compact branch uses divs for every visible part.

**8. Own event with `Cancelled` keeps `Event` in its name:** `, Event, 17:00, Cancelled` (the plan's wording); the drawing's frame shows `, Cancelled, All day`. The marker shows the true status either way.

**9. Sweep tooling (outside the repository):** `fixtures.mjs` month look-ups read the new markup; `manifest.mjs` re-asserts `cal-dm` against the navbar; `sweep.mjs` no longer aborts when a context is already gone; `probes.mjs` excludes the footer's 32px step from `pageScrolls`; `cal-nojs` was swept with `--no-probes` (the probes need JavaScript); the 15-row sweep hung twice at the end and was run in batches with `--merge`; `allow-97-14.py` and `fit-measure-97-14.mjs` are new. `cal-onerow` and `cal-nojs` carry no BEFORE record.

**10. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), where the brief named Opus 5.5.

## Known issues (recorded, not hidden)

- A six-week month at 992x720 overshoots by 24px (header 98px); it fits from 1100x800.
- Without the module, 2 to 4 crowded cells per month scroll their own list; the next slice adds "+N more".
- The phone runway-alert series link measures 22px (unchanged, as today).
- `ev-index` fixture drift above.

## Findings closed

A-events-008, 013 (partly), 014, 015, 016, 017, 018, 026, 029, 034, 037, 049, 051 are addressed by the rendered result and pinned by a fact each: icon-only markers and decorative icons are hidden from assistive technology and carry visually hidden words (21 `<i>` elements in `_Calendar`, all `aria-hidden`; names pinned in `OwnEntries_CarryTheFullAccessibleName`), named month buttons and legend, weekday header contrast kept, the compact rows reachable by keyboard as real links with a ring at offset 0, inert linked rows, the today marker, the empty note, and Quest Details contrast. The id-to-finding mapping is not re-derived here.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or schema. `CanOpen` still decides link or inert server-side (T-97-40, `StaticLinkedEntries_StayInert`); linked detail blocks hold only title, board, DM, time and status (T-97-39); detail blocks are Razor-encoded text with no id and no data attribute (T-97-41); the Quest Details vote form is untouched (T-97-55).

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 2019 passed, 0 failed (2000 at the start; +19). `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` with the user-secrets connection.
- Icon acceptance check on `_Calendar.cshtml`: `ok` (21 icons).
- No planning reference in the changed source (grep clean).

## Self-Check: PASSED

Files present: `_Calendar.cshtml`, `_LinkedDayEntries.cshtml`, `Calendar/Index.cshtml`, `calendar.css`, `CalendarMonthMarkupTests.cs`, `DesktopPageRegions.cs`; commits 34895d0c and f0c107c9 present; port 8300 stopped, 8301 untouched.
