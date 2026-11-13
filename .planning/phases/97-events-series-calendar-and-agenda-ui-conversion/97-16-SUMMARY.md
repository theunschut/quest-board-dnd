---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 16
subsystem: conversion-closure
tags: [guard-tests, counts, test-inventory, exception-record, after-capture, accessibility-bar]
requires:
  - phase: 97-15
    provides: the finished calendar module and the last converted view
  - phase: 97-05
    provides: Gate 1 answers (header buttons removed, v2 drawing)
provides:
  - "EventsConversionGuardTests.PhaseScope_IsFullyConverted and Layouts_LinkNoAreaSheet"
  - "counts-after.json: every inline-code and literal count at zero, the untouched-paths proof against both bases"
  - "tests-after.txt and tests-renamed.md: every vanished test name mapped to its replacement, no count lower"
  - "contrast-exceptions.json: three pending exceptions, five retired weekday entries, the known issues and the items for Gate 2 review"
  - "after/final: 237 records under both User-Agents, a11y.md, the accessibility-bar probes"
affects: [97-17]
actuals:
  tokens: 1000
  tasks: 3
  commits: 2
key-files:
  created:
    - .planning/phases/97-events-series-calendar-and-agenda-ui-conversion/97-16-SUMMARY.md
  modified:
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
key-decisions:
  - "The untouched-paths proof is taken against both bases: the phase start (14fcdefb) and the re-frozen baseline (73f6db87); the plan's BASELINE_SHA alone would not see 97-04 and 97-06."
  - "The final compare runs per User-Agent with per-User-Agent allow files, because the slice allow files for desktop and phone name different renames for the same row."
  - "MobileViewsTests and BoardPageStaticGuardTests are left untouched on purpose (reasons below)."
requirements-completed: [D-03, D-08, D-09, D-17, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-7, DoC-8, DoC-9, DoC-10]
status: complete
---

# Phase 97 Plan 16: Fully converted, proven and captured Summary

The events, series, calendar and agenda area is pinned as fully converted by a guard, every count behind that claim is zero and recorded, no test was lost, the Domain, Repository, feed and helper paths are untouched, and the finished build is captured under both User-Agents with the accessibility bar tabulated. One repository change: two guard facts.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: `PhaseScope_IsFullyConverted`, `Layouts_LinkNoAreaSheet`, counts, untouched paths | done | 1ca21956 |
| 2 Test inventory, shared guards confirmed, exception record | done; no repository change (see below) | none |
| 3 Full AFTER capture, accessibility table, migrator tests | done; evidence outside the repository | none |

Tracer gate: auto mode was not active. As for 97-14 and 97-15 the orchestrator asked for the whole plan, so the tracer's guard run (63 facts of the three conversion guard classes green) was done before expanding.

## Base used for every comparison

- **Phase start `14fcdefb`** is the true BEFORE: `tests-before.txt` was recorded there, and it is the only base that sees 97-04 and 97-06.
- **Re-frozen baseline `73f6db87`** (`BASELINE_SHA`, also the 8301 tree and `before/`) already contains phase 96, 97-04 and 97-06. A diff from it alone cannot show what those two plans touched, so the untouched-paths proof runs against both, and additionally over every commit whose subject carries a phase 97 plan id.
- The sweep BEFORE for the plan's compare is `before/` (baseline `73f6db87`); `before.14fcdefb/` is quoted where 97-04 and 97-06 move a number (the agenda).

## Task 1: the guard, the counts, the untouched paths

`PhaseScope_IsFullyConverted`: every area view present on disk (16 files) is in `ConvertedViews`; the seven area sheets (the six the phase scopes plus `event-forms.css`) are all present and in `ConvertedSheets`; a `.cshtml` dropped into the `Calendar`, `Agenda`, `Events` or `Series` view folder must join `AreaFiles` or the fact fails (so a new view cannot escape the scan); `UnconvertedByDecision` stays empty. `Layouts_LinkNoAreaSheet`: neither `_Layout.cshtml` nor `_Layout.Mobile.cshtml` links any of the seven sheets. Both pass; the existing conversion, foundation-layer and quest guards stay green. `tools/counts.py` in the scratch tree was the phase-95 quests counter; it was kept as `counts-quests.py.bak` and rewritten for this area.

Counts (`counts-after.json`; views are the 16 present files, BEFORE had 17 with the retired series partial; sheets 7 now, 6 before, `event-forms.css` is new):

| Metric | BEFORE (14fcdefb) | AFTER |
|---|---|---|
| style attributes | 2 | 0 |
| style elements | 0 | 0 |
| inline scripts | 7 | 0 |
| inline handlers | 18 | 0 |
| hex colours | 65 | 0 |
| rgb() colours | 71 | 0 |
| important flags | 70 | 0 |
| utility-colour rules (a rule recolouring a Bootstrap colour utility) | 2 | 0 |
| sheets opening `@layer pages` | 0 of 6 | 7 of 7 |

The plan quoted 17 handlers and 74 important flags; the counter reproduces 65 and 71 exactly and reads 18 and 70. The research figures included comments; this counter strips them, as the guards do. The two utility-colour fights (`agenda.css`, `calendar.mobile.css`) are gone; the one remaining `.text-danger` selector in `event-forms.css` sets a background, not the colour the utility owns, so it is not a fight.

Untouched paths (`counts-after.json` under `untouched`): no file under `QuestBoard.Domain`, `QuestBoard.Repository` or `QuestBoard.Migrator`, none of `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`, their two interfaces, `site.js`, `WallClockMarkupTests.cs`, `LocalTimeMarkupTests.cs`, `WallClockUnmovedTests.cs` or `SiteJsHydrationTests.cs` changed since `14fcdefb` or since `73f6db87`, and none of the 37 phase 97 commits touched them. The Service's non-view source changed by exactly three files in the phase: `CalendarController.cs` (reads the viewer id into `ViewBag`, no write), `QuestOnDay.cs`, `HtmlHelperExtensions.cs`. No new write path to Events or Quests.

`HtmlHelperExtensions.cs`: the plan's acceptance says the diff adds only the four style entries and a reworded table comment. The real diff (97-06) adds the four server-only style entries (`time-24h`, `weekday-short`, `weekday-long`, `weekday-long-year`) **and** the lookup in `BuildWallClock` that reads them with the current culture (the four entries format with the culture the replaced view code used, so text is unchanged on any host). No table comment was reworded. Recorded, not a problem.

## Task 2: no test deleted, the shared guards, the exceptions

Inventory (`tests-after.txt`, listed names, same format as `tests-before.txt`): unit 1378 to 1401, integration 1857 to 2021; no project's count dropped. Vanished names: unit 2 (a listing artefact: the 14fcdefb file broke a theory row at its U+0085 character; the rows are present unchanged), integration 24: 4 are phase 96 renames (`1381887f`/`f0a4dbc4`, `52307a1e`, `f29749a2`), 20 are phase 97 renames (97-07, 97-12, 97-13, 97-14). Every one maps to a replacement of the same intent in `tests-renamed.md`, which also lists each assertion that kept its test name but was rewritten (the `stopPropagation` count to a no-handler fact, the `setAvailability` count to the data-attribute facts, the `window.location.href` assertions to one stretched `href` and no `onclick`, and so on). Every removed assertion line in a phase 97 commit has a replacement line.

`MobileViewsTests.cs` is untouched: its Calendar and Agenda facts assert the phone view is chosen under a real phone User-Agent (`calendar.mobile.css`, `agenda-quest-entry`, `agenda-day-label`, `agenda-event-entry`, `calendar-date-entry-mobile`) and the hook classes still render; they pass unchanged and name no removed construct. `BoardPageStaticGuardTests.cs` is untouched: its `FeatureSourceFiles` list is the board-page-selection feature's own scan, with that feature's planning patterns; the new modules (`row-link.js`, `event-details.js`, `event-series-form.js`, `agenda.js`, `calendar-month.js`) are already in `EventsConversionGuardTests.PhaseSourceFiles`, which scans them for planning references.

`contrast-exceptions.json`: exactly three `entries`, all `approved.status: "pending"`, each with the measured value, a plain-language reason and the decision it rests on; five `retired` weekday entries (AFTER 5.63:1). Measured on the render (`after/final/exception-probe.json`, member and Dungeon Master, 1280x720 and 1440x900):

1. **Compact entries and "+N more": 20px** rows, 18px links, 20px buttons, against 24px (2.5.8). Desktop month at 992px and wider only. Reachable at 50.6px (71.2px for two lines) through the day panel, through the card, and by the entry's own link.
2. **Dashed linked outline: 1.66:1** against the dark cell (rgb 79,58,34 on 36,20,13), 7.72:1 against the entry's own fill. The contract carried 1.16:1 from the mockup; the production render is higher; both are under the 3:1 non-text bar.
3. **Focus ring offset 0** on compact entries and "+N more" (the button also z-index 2), **1px** on panel rows; 2px width and colours unchanged.

The file also carries two keys outside `entries` (so the "exactly three" rule holds): `knownIssues` (recorded, not fixed) and `gate2Review` (items to look at).

## Task 3: the AFTER capture

`after/final/report.json`: **237 records**, every manifest row (46) at every listed size under both User-Agents, none non-200, **0 console errors, 0 page errors, 0 failed requests, 0 action failures, 0 assert failures**. The month and agenda ran in 8 batches with `--merge`; the first batch process finished and wrote its report but did not exit, and was ended by hand (the hang the brief warns about). `cal-nojs` (JavaScript off) was swept with `--no-probes`, as in 97-15: a JavaScript-off page hangs the probe's style injection.

`readability-compare` against BEFORE (`before/`), per User-Agent with `--zero-contrast phase` and the exception file:

- **Zero residual contrast failures** on every phase row, both User-Agents, every width, with no exception needed (the three recorded exceptions are not contrast failures the probe measures).
- Everything else is clean except two attributed items, neither a code change:
  - `ev-index-dm` and `ev-index-roster` (desktop 320, 1024, 1280, 1440 and phone 390, 412): "Candlemas Feast at the Hollow" is absent from the list. A second `Tallow Day Gathering` fixture event pushes it off. The fixture drift, as recorded in 97-14 and 97-15.
  - `cal-dm` phone 412: "new small target", the series link inside the runway alert (22px, inline). BEFORE measured the same link as a 46px box because it wrapped over two lines; it now fits on one line. Pre-existing, listed below.
- Why the compare is per User-Agent: the slice allow files for desktop and phone name different renames for the same row id, so a single merged file made phone controls "missing". `allow.json` is the plain union; `allow-desktop.json` and `allow-mobile.json` are what the compare used (desktop adds the `calm-*` rows by copying the matching `cal-*` mapping, as they show the same page at 1440).

Content contrast failures (in main), summed over the records of each page group, BEFORE at the baseline `73f6db87`, BEFORE at the phase start `14fcdefb`, AFTER:

| Page group | Rows | BEFORE 73f6db87 | BEFORE 14fcdefb | AFTER |
|---|---|---|---|---|
| Calendar month and phone agenda | 17 | 1256 | 1256 | 0 |
| Quest Details calendar | 5 | 88 | 88 | 0 |
| Events overview | 4 | 108 | 108 | 0 |
| Event details | 4 | 32 | 32 | 0 |
| Event forms | 6 | 72 | 72 | 0 |
| Series | 3 | 4 | 4 | 0 |
| Agenda | 7 | 316 | 236 | 0 |
| Total | 46 | 1876 | 1796 | 0 |

(The agenda's extra 80 at the baseline are the wall-clock `<time>` wrapper on the muted parent that 97-06 introduced.)

Events/Details at 390: BEFORE at `14fcdefb` overflowed by 4px (scroll width 394; 416 at 412); at `73f6db87` and AFTER none (390 and 412), for all four rows.

Fit (month, desktop, the final sweep reproduces the 97-15 table): no page scroll and no overflowing cell at 1280x720, 1366x768, 1440x900, 1536x864, 1920x1080, 1280x1024 and 1280x700 on the six-week, five-week, empty and current months. The runway-alert rows scroll by the alert's height and clip nothing: `cal-dm` 97, 49, 49, 85, 1 (1920x1080), 57, 0 (1280x700) and `cal-dm-alert` 73, 25, 25, 61, 0, 33, 0 at 1280x720, 1366x768, 1440x900, 1536x864, 1920x1080, 1280x1024, 1280x700. At 991, 768 and 320 no horizontal overflow on any calendar row. The six-week month at 992x720 overshoots by 24px (known; fits from 1100x800), not in the sweep (the sweep's narrow size is 991).

Phone small targets: 4 records report one, and it is the same pre-existing inline link: the series link inside the runway alert (`cal-dm`: Gloamhaven Weekly Muster 225x22; `cal-dm-alert`: Tidewatch Rota 124x22, each at 390 and 412). Every other phone record reports none.

`after/final/a11y.md`: one row per page and layout (91 rows) tabulating A-1 to A-11 from the sweep plus three probes written for this plan (`a11y-probe-97-16.mjs` headings, names, alerts, animations; `a11y-focus-97-16.mjs` tab-through focus-not-obscured with instant scrolling; `a11y-reduced-97-16.mjs` reduced motion). Totals: 0 contrast failures, 0 missing focus rings, 0 obscured focus stops, 0 horizontal overflow and 0 text-spacing overflow on any record, one `h1` on every page and layout, 0 unnamed controls, 0 infinite animations, 0 `transition: all` with a duration, 0 transitions over 1ms and 0 animations running under reduced motion, 0 toasts written by an area view. The `role="alert"` counts are the persistent runway notice on the Calendar and Series pages, not a transient message. A-11: 2.5.7 dragging, 3.3.7 redundant entry and 3.3.8 accessible authentication are not applicable with the reasons stated; 1.4.12 text spacing is checked on every record. Contrast claims on the blurred panels are the unblurred computed figures, re-proved on the rendered page. Two probe artefacts were checked and excluded, not hidden: hidden radio inputs drawn by their own label and the editor's hidden textarea sit under their own control, and the JavaScript-off page was re-walked with a longer settle time (the first walk caught the page mid smooth-scroll).

## Verification

- `dotnet test QuestBoard.UnitTests`: **1412 passed**, 0 failed.
- `dotnet test QuestBoard.IntegrationTests`: **2026 passed**, 0 failed (2024 at the start; +2, the two guard facts). `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` with the user-secrets connection, no CLR flake.
- Migrator tests run once with the flag: 11 passed (the database at localhost:1433 was reachable; nothing started or provisioned). The feed-revision, calendar-subscription, wall-clock-unmoved and site-script hydration facts: 139 passed.
- No planning reference in the changed source (the guard fact covers the file).
- Port 8300 stopped; 8301 untouched and still listening.

## Deviations from Plan

**1. [Plan text] Bases.** The plan's untouched-paths check reads from `BASELINE_SHA` (`73f6db87`), which moved after 97-04 and 97-06 landed; those two plans are therefore checked against `14fcdefb` and by commit. Result unchanged: nothing protected changed.

**2. [Plan text] Counts.** Handlers 18 (plan 17) and important flags 70 (plan 74); the plan's figures included comments. Hex 65 and rgb 71 match.

**3. [Plan text] `HtmlHelperExtensions.cs` diff.** Adds the four style entries and the lookup that reads them, not a reworded table comment (see above).

**4. [Plan verification] Broken commands.** The test-inventory check reads `count:\s*(\d+)`, which does not match the `count: <Project> <N>` lines of the inventory file, and the compare command passes `--pages phase`, which selects a row named "phase" and compares nothing. Both were replaced by working equivalents (own comparison script; no `--pages`, with `--zero-contrast phase`).

**5. [Plan scope] No repository change in Task 2.** `MobileViewsTests.cs` and `BoardPageStaticGuardTests.cs` needed none (reasons above), as the plan allows.

**6. [Plan truth not met literally] "No small target on any phase phone record".** Four records report the inline runway-alert link at 22px (pre-existing, recorded). Nothing else.

**7. [Plan truth] A-5 on desktop.** The contract says at least 24px on desktop apart from exception 1. The sweep shows pre-existing controls under 24px outside the month, unchanged from BEFORE: Events overview event title links (22px), Series occurrence View links (35x22), the day quest links in the Quest Details calendar (22px), the Agenda board filter checkboxes (21px), the editor's hidden textarea. Recorded in `knownIssues`, not fixed, not added as an exception (a new exception needs the operator).

**8. [Tooling, outside the repository] New or changed:** `counts.py` (rewritten), `untouched.py`, `a11y-probe-97-16.mjs`, `a11y-focus-97-16.mjs`, `a11y-reduced-97-16.mjs`, `a11y-focus-slow-97-16.mjs`, `a11y-table-97-16.py`, `exception-probe-97-16.mjs`.

**9. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), as in 97-15, where the brief named Opus 5.5.

**10. `.claude/ui-guidelines.md` line 63** still says `calendar.css` is not in a layer. The plan lists no documentation file, so it is recorded, not corrected.

## For Gate 2 review

- 97-15 loosened `CalendarSheet_KeepsMonthAndQuestDetailsRulesApart` to also accept `.cal-card`, `.cal-panel` and the keyframe `from` and `to` steps, because the overlays are mounted on the body. The check is a prefix match on two roots only the month renders; the month-versus-Quest-Details protection is unchanged. It is the one guard in the phase whose strictness went down.
- The visible change on Quest Details: a called-off linked chip is now muted ink on quieter paper.
- Board Availability and My Agenda left the Calendar page header (approved at Gate 1); both stay in the navigation; the Events pages keep My Agenda.
- The Maybe badge lost its `text-dark` utility (the pair comes from the foundation).
- The three exceptions are pending until the operator signs them at Gate 2.

## Known issues (recorded, not fixed)

- 20px compact month rows and "+N more" (exception 1; reachable through the 48px-plus panel rows and the card).
- The dashed linked outline is a non-text exception (exception 2).
- A six-week month at 992x720 overshoots by 24px; it fits from 1100x800.
- The phone runway-alert inline link is 22px (pre-existing).
- The `ev-index` fixture drift (`Tallow Day Gathering`).
- Resizing across a height band closes an open card or panel.
- The panel heading has no year (the drawing shows one).
- An own cancelled event is named ", Event, 17:00, Cancelled" (the drawing shows ", Cancelled, All day").
- Desktop controls under 24px outside the month (deviation 7); below 768px the marker-tile link is 22px inside its 24px tile.
- `.claude/ui-guidelines.md` line 63.

## Known Stubs

None.

## Threat Flags

None. No endpoint, auth path or schema change; the only repository change is a guard test. T-97-46 (data and feed paths): no Domain, Repository or Migrator file and none of the feed files changed, the feed-revision guards and the migrator tests pass. T-97-47 (weakened guards): every vanished test name is accounted for in `tests-renamed.md`, no count dropped, the one loosened guard is flagged above. T-97-48 (exceptions treated as approved): all three entries carry `pending`. T-97-49 (operator database): the migrator tests ran once; nothing was started, stopped or provisioned.

## Self-Check: PASSED

Files present: the guard test, this summary, `counts-after.json`, `tests-after.txt`, `tests-renamed.md`, `contrast-exceptions.json`, `after/final/report.json`, `after/final/a11y.md`, `after/final/allow.json`; commit 1ca21956 present; STATE.md, ROADMAP.md and REQUIREMENTS.md untouched; port 8300 stopped, 8301 untouched and still listening.
