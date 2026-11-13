---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 13
subsystem: calendar-desktop-css
tags: [css-layers, design-tokens, guard-tests, quest-details, computed-style-diff]
requires:
  - phase: 97-05
    provides: Gate 1 answers and the approved desktop calendar drawing (version 2)
  - phase: 97-12
    provides: phone calendar twin and the viewer id on the calendar page
provides:
  - "calendar.css as one @layer pages block, token-only, no important flag, no transition: all"
  - "Month rules scoped .calendar-page and Quest Details rules scoped .details-page; the weekday header is the only shared rule"
  - "A wood weekday header (parchment on wood, 5.63:1) on both pages"
  - "calendar.css linked from Calendar/Index's Styles section instead of the global layout"
affects: [97-14]
actuals:
  tokens: 16000
  tasks: 2
  commits: 1
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarStyleTests.cs
key-decisions:
  - "The month and Quest Details each carry their own copy of the grid rules (.calendar-page and .details-page), so the compact month in the next slice can replace the month half wholesale without touching the vote layout."
  - "Colours with no matching token map to the nearest role token (ink, ink-muted, success, info, neutral); the accepted pairs are an explicit table, not an accident."
  - "The checked vote choice keeps its clear edge (an explicit rule), so Quest Details renders exactly as before."
requirements-completed: [D-08, D-11, D-15, D-17, DoC-2, DoC-3, DoC-5, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 13: Calendar sheet into the pages layer Summary

`calendar.css` is now one layered, token-only sheet loaded only by the Calendar page and Quest Details, with today's month look
kept, the 52 important flags gone, and the weekday header a readable wood band on both hosts.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: layered sheet, wood weekday header, per-view link, global link removed, guard arrays | done | 12ffaa2a |
| 2 Quest Details computed-style comparison on both layouts, retired weekday exceptions | done (scratch tooling and records only; no repository change) | none |

Tracer gate: auto mode was not active, so the plan's interactive stop would have applied; the orchestrator asked for a full
run, so the slice was verified before expansion instead (guard and Calendar suites 488 passed, the month swept with zero
readability regressions) and Task 2 followed.

## Sheet counts (comments stripped)

| | before | after |
|---|---|---|
| lines | 860 | 1243 (month and Quest Details halves are written twice on purpose) |
| `!important` | 52 | 0 |
| hex colours | 32 | 0 |
| `rgb(` | 34 | 0 |
| `transition: all` | 5 | 0 |
| layer | none | `@layer pages` |

`_Layout.cshtml`: `grep -c calendar.css` 1 before, 0 after; `legacy-pages.css` is still linked there.

## Selector scan

`selector-scan.py` over every class the sheet defines found no use outside `Calendar/Index`, `Shared/_Calendar`,
`Shared/_LinkedDayEntries` and `Quest/Details`. No rule had to move to `components.css`. Dead rules found and removed:
`.quest-date-indicator` (+ `.badge`), `.finalized-icon` (details block), the three `.vote-buttons .btn-check:checked +
.btn-outline-*` rules (no such label exists in the markup), `.details-page .calendar-grid` (the class sits on the grid
itself, so the selector never matched), `.legend-card .card-body { padding }` (Bootstrap's `p-2` already won; computed padding
is unchanged), `.quest-title` in the small-window block, and four small-window hover font sizes for the DM line and time that
the old important flags had always overridden.

## Quest Details style diff

`~/qb-ui-review/phase-97/tools/calendar-style-compare.mjs` walks every element inside `.calendar-grid`, `.vote-buttons`,
`.calendar-vote-row`, `.calendar-linked-note`, `.calendar-date-entry-mobile` (plus `.legend-card` and `.calendar-navigation` for
the month) on the frozen baseline (8301) and the working tree (8300), at rest and while the pointer rests on a plain quest chip,
the current quest, an event chip and a linked chip, over 70 properties, normalising colours. `qd-open, qd-voting, qd-update,
qd-spectator, qd-finalized`: desktop 1440, 768, 600, 320, and the phone.

Result (`after/97-13/qd-style-diff.json`): **0 differences, 0 structure changes.** What it reports besides:

- **allowed (3808)**: the weekday header and its day cells only (colour, fill, 48 to 24px height, 16 to 14px type, shadow gone), plus
  on the phone the three elements 97-12 already changed (the 44px also-that-day link and the entry box it grows); the phone
  sheet does not load `calendar.css`, so this slice does not touch it.
- **consequential (64)**: the grid's own height, which drops by exactly the header's height change.
- **mapped colours (30,960)**: colour-only changes from moving onto tokens (below).

The same comparison on the Calendar page month (`cal-crowded, cal-current, cal-dm, cal-empty`, widths 1440 to 320, with hover
states) and on the legend and navigation: 0 differences (the only allowed extra there is the viewer's own vote marker on the
desktop month, added when 97-12 supplied the viewer id).

### Token-mapped colours (the one place the pixels move)

The plan asks for tokens only and for no computed change; the two cannot both hold for eight legacy colours that have no
token. They map to the closest role token, as this explicit table (`after/97-13/colour-map.json`); nothing else changed colour:

| Legacy | Token | Where |
|---|---|---|
| `#1a1a1a`, `#262626` | `--qb-ink` | chip, linked-entry text |
| `#666` | `--qb-ink-muted` | DM line, time |
| `#28a745` | `--qb-success` | finalized edge |
| `#3498db` (solid and 10 or 50 percent) | `--qb-info` | current-quest edge, today cell |
| `#6c757d` | `--qb-neutral` | linked legend dash |
| `#ffc107`, `#6f42c1`, `#f4e4bc`, whites and blacks with alpha | exact token or mix | no change |

Each is a few channel values (a slightly darker ink, a deeper green and teal-blue edge). Ink text on the chips measures higher than
before. Worth a glance at the next review if it matters.

## Weekday header and retired exceptions

`.calendar-header` is `--qb-wood` with `--qb-fg` text; `.calendar-day-header` is 14px, 600, 24px row (12px and 11px below 768 and
480px, where seven columns hold the full day names). Computed ratio **5.63:1**; the contrast table in `UiFoundationLayerGuardTests`
now carries the parchment-on-wood pair. BEFORE was 3.76:1 on every desktop Quest Details record. The sweep (`qd-*`, 22 records)
reports no weekday failure on any record. The five Phase 95 `div.calendar-day-header` entries were copied to
`~/qb-ui-review/phase-97/contrast-exceptions.json` under `retired` with the AFTER ratio and the "now a wood fill" note;
`entries` holds none. Phase 95's own file is untouched.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed.
- `dotnet test QuestBoard.IntegrationTests`: 2000 passed, 0 failed (1996 at the start; +4 guard facts, the wood contrast pair is a new row of an existing fact). QUESTBOARD_MIGRATOR_TEST_REQUIRED=1 with the user-secrets connection.
- Sweep `cal-crowded, cal-current` (24 captures, all 200) into `after/97-13`; `readability-compare` with the phone allowances
  (renamed `main` key, removed `My Agenda`, both from 97-12): 20 compared, **0 failing rows**. Desktop month reads as before with
  the readable weekday header (screenshot reviewed at 1440).
- Sweep `qd-*`: 46 records in the merged report, all 200, no overflow; contrast failures left on Quest Details are the linked-entry
  titles only (see Known issues).

## Deviations from Plan

**1. [Rule 3 - Blocking] The linked-entry style facts followed the scoped selectors.** `LinkedBoardCalendarStyleTests` finds a rule
by its whole selector, so the month and Quest Details copies (`.calendar-page .linked-entry`, `.details-page .linked-entry`) made six
facts find nothing. Each fact now asserts the same declarations under both scopes. Nothing weakened; the file is outside the plan's
file list. Commit 12ffaa2a.

**2. [Rule 2 - Missing critical functionality] New guard facts** in `EventsConversionGuardTests`: the sheet is not in the global
layout and is linked by both pages, no `transition: all`, every rule is the shared weekday header or sits under one of the two scopes,
and the weekday header is wood with parchment text and no shadow. Plus the wood pair in the contrast table. Commit 12ffaa2a.

**3. Token mapping changes eight legacy colours slightly** (table above). The plan's "no differences outside the weekday header" is
met for every property except those colour values, which the diff tool reports separately as `mappedColours` against an explicit table.

**4. The checked vote choice keeps a clear edge.** Under the layer, Phase 95's `quest-vote.css` rule that gives the checked choice a dark
edge would start to win over the calendar sheet's transparent border, a visible change. A one-line rule keeps today's look. Phase 95's edge
was in effect never shown; whether to show it is a separate Quest Details decision.

**5. Wrapper class sits on the page's existing root row** (`div.row.calendar-page`) rather than a new wrapping div, so the markup and
layout stay identical.

**6. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), where the brief named Opus 5.5.

**7. Tooling outside the repository:** `tools/calendar-style-compare.mjs`, `after/97-13/colour-map.json`, `allow.json`, diff reports
(`qd-style-diff.json` final; `-sanity`, `-1`, `-2` are the iterations).

## Known issues (recorded, not hidden)

- Quest Details desktop: `div.linked-entry-title` and its hidden span measure 3.10:1 on four records (2.87:1 BEFORE; slightly better
  with the ink token). They are dimmed cancelled linked entries; the linked rows belong to the compact month slice.
- The weekday header columns on Quest Details and the month at 768px and narrower size by their own text (`1fr` with a content minimum), so
  they are not aligned with the day columns below at 600 and 320px; the same as before.
- The phone Quest Details also-that-day link and entry box differ from the frozen baseline because 97-12 changed them, not this plan.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path, file access or schema; Quest Details markup and vote inputs are untouched (T-97-42), and the sheet moved
off the global list only after the selector scan and the full sweep showed nothing else needs it (T-97-54).

## Self-Check

Files: `calendar.css`, `Calendar/Index.cshtml`, `_Layout.cshtml`, the three test files, this SUMMARY. Commit 12ffaa2a present.

## Self-Check: PASSED
