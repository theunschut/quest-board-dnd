---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 07
subsystem: events-overview-ui
tags: [razor, css-layers, availability, stretched-link, row-module, guard-tests]
requires:
  - phase: 97-05
    provides: Gate 1 package and the sweep tooling
  - phase: 97-06
    provides: wall-clock helper on every area date, the area guard class
provides:
  - "Events/Index (desktop and phone) and the two availability partials on the layered foundation, no inline code"
  - "The availability vocabulary defined once in components.css (cells, counts) with ink tones on paper and parchment on glass"
  - "row-link.js: whole-row mouse navigation from tr[data-row-href]"
  - "events-overview.css and events-overview.mobile.css as one @layer pages block each, off the global stylesheet list"
  - "EventsConversionGuardTests lists and facts the later slices extend"
affects: [97-08, 97-09, 97-10, 97-11, 97-12, 97-13, 97-14]
actuals:
  tokens: 16750
  tasks: 3
  commits: 3
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/row-link.js
  modified:
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml
    - QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/wwwroot/css/events-overview.css
    - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
    - QuestBoard.Service/wwwroot/css/agenda.css
    - QuestBoard.Service/wwwroot/css/agenda.mobile.css
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/EventsOverviewMobileStyleTests.cs
    - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs
key-decisions:
  - "On glass the empty dash and the count detail take the full parchment colour, not the muted token: the muted tone measured 4.25:1 on a card body over the brightest board pixel. The muted token is used on paper (ink-muted, 8.5:1) and for the dashed edge, which is not text."
  - "Count headline is the 20px heading step (was 28px): the display step is reserved for the page title."
  - "The phone card keeps the resting glass on hover: a tap leaves a sticky hover, and the lighter hover tint measured 4.34:1."
requirements-completed: [D-15, D-17, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-7, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 07: Events overview and the availability partials on the foundation Summary

Both Events/Index layouts and the two shared availability partials render on the layered foundation with no inline
handler, style or script: desktop rows open through a small ES module, phone cards through one stretched link, the
five availability cell states and the attendance counts are defined once in `components.css`, and
`events-overview.css` no longer loads on every page.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: desktop Events overview, row module, sheet link, guard lists, rendered fact | done | 3e9c0d15 |
| 2 Phone Events overview, handler-free roster toggles, rewritten guards | done | 67f8347e |
| 3 events-overview.css off the global list, sweep and compare against BEFORE | done | f761f6e2 |

Tracer gate: the tracer verify (guard, layer, row-navigation, overview controller and mobile-style facts, 59 then 25
rows) passed and the desktop page was re-rendered under Chromium on 8300 before the phone work began; auto mode was off
in config, but the run was spawned as an autonomous executor with no resume, so the gate was run as the autonomous
form (re-verify, halt on failure) instead of returning a checkpoint.

## What changed

- **Desktop Events/Index**: h1 `Board Availability` in the page-title card (gold from the foundation), `Legend` as an h2
  in a `.modern-card--aside` card, `.empty-state` with the verbatim copy, `tr.avail-row-clickable[data-row-href]` with
  no handler, `a.row-nav-link` kept as the keyboard path, decorative icons `aria-hidden`, `@section Styles` and
  `@section Scripts`. The sticky pair and the viewer's column now ride the opaque paper table fills (the old `!important`
  cell-fill fights are gone).
- **Phone Events/Index**: card is `.avail-card.modern-card.modern-card--interactive` (unblurred), title `h2 > a.row-nav-link`
  stretched by `::after`, ring on the card via `:has`, `Show players` (`data-bs-toggle="collapse"`, 44px) and the roster
  at `position: relative; z-index: 1`; no `onclick`, no `stopPropagation`. Legend stays behind `What do these mean?`
  (now `aria-controls`); no `Attendance` label.
- **Partials**: `text-dark` dropped from the Maybe badge, `text-muted` dropped from the empty dash and the count detail,
  icons `aria-hidden`; titles, `aria-label`s and words verbatim.
- **components.css**: `.avail-cell-yes-muted` (success fill clipped to the padding box so the dashed edge reads against
  the surface, `--qb-ink-muted` edge on paper, `--qb-fg-muted` on glass), `.avail-cell-empty`, `.avail-count-summary`,
  `.avail-count-headline`, `.avail-count-detail`; the old `.avail-card` secondary-colour override is removed.
- **Sheets**: `events-overview.css` (grid layout, sticky columns, own-column tint, clickable cursor, empty-state sentence)
  and `events-overview.mobile.css` (card, stretched link, ring, toggle) each open `@layer pages`, tokens only, no
  `!important`, no media query, no title typography. The old `legend-card` compact sizing (12px text) is gone: legend
  text is the 14px step.

## Guards rewritten (old -> new)

- `EventsOverviewMobileStyleTests.MobileOverviewCss_AvailCard_UsesGlassSurfaceNotOpaqueSlab` -> `..._AvailCard_AddsNoRepeatedBackdropBlurOrOpaqueSlab` (no `backdrop-filter`, no opaque slab colours; the rendered class fact now also pins `modern-card modern-card--interactive`)
- `..._CountBlock_SetsExplicitLightColour` -> `..._CountBlock_TakesItsColourByTokenOrInheritance` (count rules in the phone sheet and `components.css` set a `var(--qb-*)` colour or none, no `!important`)
- `..._ExpandToggle_KeepsMinimumTapTarget` kept: now reads the `.avail-expand-toggle` rule for `min-height: var(--qb-hit-min)`
- `EventsOverviewControllerIntegrationTests`: the `event.stopPropagation()` count of 2 inside `Index_MobileUserAgent_RendersRosterToggle` -> new `MobileOverview_RosterToggle_OpensWithoutAnyHandler` (toggle and `#roster-{id}` carry no `onclick`, toggle carries `data-bs-toggle="collapse"` and the target); new `Index_Desktop_RowsCarryTheirAddressForTheRowModuleAndNoInlineHandler`
- `RowNavigationAccessibilityTests`: one stale comment reworded, assertions unchanged
- `UiFoundationLayerGuardTests`: `events-overview.css` and `events-overview.mobile.css` appended to `LayeredFiles` ("pages") and `NoLiteralFiles` (append only)
- `EventsConversionGuardTests`: `ConvertedViews` (Events/Index, Events/Index.Mobile, both partials), `ConvertedSheets`, `ViewSheetLinks` and the facts `ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `ConvertedSheets_OpenThePagesLayer`, `ConvertedSheets_HaveNoImportantFlag`, `ConvertedSheets_HaveNoColourLiteral`, `ConvertedViews_LinkTheirSheets`, `RowLinkModule_WritesNoMarkupFromStrings`, `AreaSheets_DoNotRestyleCardTitles` (with self-test lines in the scanner fact); `PhaseSourceFiles` extended

## Global link removed, selector scan

`events-overview.css` was deleted from the `_Layout.cshtml` list (no comment left). `selector-scan.py` over both the old and
new sheet: every class they define (`avail-grid`, `avail-col-*`, `avail-sticky-col`, `avail-row-clickable`, `avail-cell-*`,
`avail-count-*`, `avail-empty-state`) is used only by `Views/Events/Index.cshtml`, `_AvailabilityCell.cshtml` and
`_AvailabilityCounts.cshtml`. No class had to move to components beyond the availability vocabulary already planned; the
Agenda views reach the cell and count rules through the shared partials.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed, 0 failed.
- `dotnet test QuestBoard.IntegrationTests`: 1937 passed, 0 failed (1928 at the start, +9), 3 m 13 s, no CLR flake.
- Sweep `ev-index, ev-index-dm, ev-index-empty, ev-index-roster, agenda, agenda-dm` into `after/97-07` (32 records, all 200),
  `readability-compare` against `before` (the 73f6db87 baseline) with `--zero-contrast` on the four Events rows: **ok,
  0 failing rows, 0 residual contrast**.
- Every ev-index record (320/1024/1280/1440 desktop, 390/412 phone): `overflow: false`, empty `focusRingMissing`, empty
  `smallTargets` under the phone UA, empty `blurredRepeats` on phone, no console or page errors; desktop h1 token `accent`.
- Findings closed: the only Events contrast failure in BEFORE was the empty availability dash (`span.text-muted.avail-cell-empty`,
  2.39 to 2.84:1 across rows, 2.28:1 on the phone agenda); AFTER it is 0 failures on the Events rows (ink-muted on paper
  8.5:1, `--qb-fg` on glass) and the phone agenda dash is above 4.5:1 (white token). The cream-on-cream count (1.06:1) and
  the Maybe badge labels did not reproduce as failures in the 73f6db87 BEFORE records (the foundation had already
  moved them), so they are closed by construction: counts inherit the surface colour (ink on paper 14.94:1, detail
  ink-muted 8.5:1), Maybe label `--qb-warning-fg` on amber 8.92:1, Yes label 5.35:1.

## Deviations from Plan

**1. [Rule 3 - Blocking] Availability vocabulary and partial edits landed in the first commit, not the second**
- The desktop sheet cannot pass its no-literal/no-`!important` facts while it still holds the old cell and count
  copies, so the move to `components.css` and the two partial edits were done with the tracer. Task 2 then covered the
  phone side. Final state matches the plan.

**2. [Rule 1 - Bug] `agenda.mobile.css` copies of the cell rules removed now, not in the Agenda slice**
- **Issue:** removing `text-muted` from the empty dash turned the phone agenda's unlayered `opacity: .55` copy into a
  regression the compare flagged (the dash on the agenda card). The shared rules replace the copies, so the two cell
  rules and their comment are deleted from `agenda.mobile.css`, one rule adds `color: var(--qb-on-fill)` for the dash on
  the agenda card's own lighter glass (4.34:1 with parchment), and the stale sentence in `agenda.css` was reworded.
  The Agenda slice still owns the rest of both agenda sheets.

**3. [Rule 1 - Bug] Contrast measured under the planned tokens**
- Muted parchment on a card body failed (4.25:1) for the empty dash, the count detail and the empty-state sentence, and
  the interactive card's hover tint failed (4.34:1); handled as in key-decisions, with a one-rule page override for
  the empty-state sentence (`.avail-empty-state`) and a resting-glass hover rule on `.avail-card`.

**4. [Tooling] `probes.mjs` focus-ring host list extended with `.avail-card`**
- The sweep's ring probe only recognises card hosts it names; the new stretched-link card was reported as "no ring". One
  selector was appended to the list in `~/qb-ui-review/phase-97/tools/probes.mjs` (outside the repository).

**5. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), where the brief named Opus 5.5.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. `row-link.js` only assigns a server-rendered `data-row-href` (Razor-encoded `Url.Action`) to `location.href`, builds no
markup (pinned by `RowLinkModule_WritesNoMarkupFromStrings`); no feed or write path was touched (`git diff` of the feed files is empty).

## Self-Check: PASSED

Created and modified files exist; commits 3e9c0d15, 67f8347e and f761f6e2 are on `feature/phase-97`; both suites green; STATE.md,
ROADMAP.md and REQUIREMENTS.md untouched; nothing pushed.
