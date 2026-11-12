---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 11
subsystem: agenda-ui
tags: [razor, css-layers, es-module, modal, guard-tests]
requires:
  - phase: 97-05
    provides: sweep tooling and the Gate 1 answers
  - phase: 97-07
    provides: shared availability vocabulary in components.css, guard lists
provides:
  - "Agenda/Index and Agenda/Index.Mobile aligned on the foundation, twins kept (Gate 1 merge answer: false)"
  - "agenda.js: fills the switch-board dialog from the trigger's data attributes"
  - "agenda.css and agenda.mobile.css as one @layer pages block each; agenda.css off the global list"
affects: [97-12, 97-13, 97-14]
actuals:
  tokens: 21000
  tasks: 3
  commits: 2
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/agenda.js
    - QuestBoard.IntegrationTests/Tests/AgendaConversionTests.cs
  modified:
    - QuestBoard.Service/Views/Agenda/Index.cshtml
    - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/agenda.css
    - QuestBoard.Service/wwwroot/css/agenda.mobile.css
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.IntegrationTests/Tests/EventsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
key-decisions:
  - "Phone rows are slips on the darker scrim inside the page's one title card, not .modern-card: no panel blur on repeats, and the muted dates hold contrast on the extra scrim."
  - "The desktop filter dropdown gets the overlay surface through its own page class; the foundation only themes the navigation dropdown, so the help sentence was parchment on Bootstrap's white menu (1.39:1)."
  - "Empty-state sentence takes the full parchment colour on the card body (muted measured under 4.5:1 there), as on the Events overview."
requirements-completed: [D-13, D-15, D-17, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-7, DoC-8, DoC-10]
status: complete
---

# Phase 97 Plan 11: Agenda twins on the foundation Summary

Both Agenda layouts now sit on the layered foundation with no inline script, style or handler: the switch-board dialog
is filled by `agenda.js`, named by its own `h2`, on the foundation modal surface, the phone roster opens without any
click guard, and each sheet is a single tokens-only `@layer pages` block loaded only by its own view.

## Merge answer applied

`gate1/answers.json` has `agenda.merge: false`, so the precondition held and the twins were kept: desktop keeps the
filter dropdown and an always-visible roster; phone keeps the filter collapse and the roster behind `Show Roster`.
`AgendaMobileRenderTests` (11 facts) pass unchanged.

## Tasks

| Task | Result | Commit |
|---|---|---|
| 1 Tracer: `agenda.js`, named foundation dialog, module/dialog facts, guard entry, Chromium dialog probe | done | 6b5ede7f |
| 2 Twins aligned, handler-free rosters, layered sheets, `agenda.css` off the global list, guard lists | done | 57711283 |
| 3 Sweep of seven states on both User-Agents against BEFORE | done (one fix, included in 57711283) | 57711283 |

Tracer gate: the dialog probe (`tools/agenda-switch-probe.mjs`, Chromium, p97player on Emberfall Wardens, both
User-Agents) read the four filled values equal to the trigger's attributes (`15`, `/Events/Details/57?from=agenda`,
board name in both spans), `aria-labelledby` resolved to the `H2`, Cancel closed the dialog with no navigation, the
row set and Open/Switch controls were unchanged after reload, and there were no console errors. Expansion followed.

## What changed

- **Both views:** page card with `.modern-card-header` holding `h1` `My Agenda` and `.header-subtitle` (gold on desktop,
  parchment on the phone, from the foundation); `Board Availability` kept for a DM when the board serves Events;
  `h2` row titles (the phone `h4` and desktop `strong` are gone); decorative icons `aria-hidden`; three empty states as
  `.empty-state` with an `h2` and the verbatim copy; `Open Event` and `Switch & Open` `btn-info btn-sm`; filter `Apply`
  and `Show More Events` `btn-secondary`; no page-level `btn-primary`.
- **Desktop:** filter menu class `agenda-filter-menu` (min-width 16rem) replaces the inline style; rows are paper
  surfaces with ink text; roster always visible.
- **Phone:** roster toggle and `#roster-{id}` lose `onclick`/`stopPropagation`; slips (`.agenda-card`) sit inside the page
  card on the scrim, not `.modern-card`; filter rows are `.form-check` at 44px with the label as the whole target.
- **Dialog (both):** `aria-labelledby` on `.modal`, `h2.modal-title#switchBoardModalLabel`, no `bg-dark`, `text-light`,
  `border-secondary` or `btn-close-white`; Cancel `btn-secondary`, Switch & Continue `btn-primary`; still posts with the
  antiforgery token to `GroupPicker/SelectGroup` with `groupId` and `returnUrl`.
- **Sheets:** each opens `@layer pages`, tokens only, no hex/rgb, no `!important`; the old `.avail-*` copies, the
  `.text-muted` override and the text-shadow halo are gone. Hook classes the tests pin are kept.

## Global link removed, selector scan

The `agenda.css` line was deleted from `_Layout.cshtml` (`legacy-pages.css` is still linked), and the desktop view
links it through `@section Styles`. `selector-scan.py` over the old and the new sheet: every class they define
(`agenda-row*`, `agenda-roster`, `agenda-roster-entry`, `agenda-filter-menu`, `agenda-empty-state`) is used only by
`Views/Agenda/Index.cshtml` and `Index.Mobile.cshtml`; no shared rule had to move to `components.css`. No other file
references `agenda.css` or `agenda.mobile.css`.

## Tests added or changed

- `AgendaConversionTests` (new, 12 rows): `Agenda_BothLayouts_LoadTheModuleAndNoInlineScript`,
  `SwitchDialog_IsNamedAndOnTheFoundationSurface`, `Agenda_BothLayouts_ShareTheHeadingOutline`,
  `Agenda_EmptyStates_AreVerbatim`, `Agenda_PhoneRoster_OpensWithoutAnyHandler`, `Agenda_ButtonRoles_LeaveNoPagePrimary`
  (theories over both User-Agents where the layout matters).
- `EventsConversionGuardTests`: `AgendaModule_WritesNoMarkupFromStrings`; both views appended to `ConvertedViews`, both
  sheets to `ConvertedSheets`, both view-sheet pairs to `ViewSheetLinks`, `agenda.js` and both sheets to `PhaseSourceFiles`.
- `UiFoundationLayerGuardTests`: both sheets appended to `LayeredFiles` ("pages") and `NoLiteralFiles` (append only).
- No existing assertion changed.

## Verification

- `dotnet test QuestBoard.UnitTests`: 1412 passed, 0 failed.
- `dotnet test QuestBoard.IntegrationTests`: 1986 passed, 0 failed (1974 at the start, +12), 3 m 19 s, no CLR flake
  (run with the migrator connection and `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`).
- Sweep `agenda, agenda-dm, agenda-noboards, agenda-noupcoming, agenda-filtered-out, agenda-filter-open,
  agenda-switch-modal` into `after/97-11` (32 records, all 200): `readability-compare --zero-contrast` on all seven rows:
  **0 failing rows, 0 residual contrast**; every record `overflow: false`, no phone `smallTargets`, no
  `blurredRepeats`, no `focusRingMissing`, no console or page errors, desktop `h1` token `accent`, phone `fg`.
- `agenda-filter-open` phone: each filter row and label 44px tall (probe); roster toggle opens and closes with no handler.

## Findings closed

The plan lists A-events-003, 004, 005, 007, 023, 028, 030, 032, 035 and 038. The evidence that closes them is the
rendered result, not a per-id mapping re-derived here: the +80 BEFORE agenda contrast failures (wall-clock time on the
muted parent) are 0 in AFTER; the phone filter rows are 44px (BEFORE record 24px in the audit); the empty states, the
heading outline, the button roles, the dialog name and surface, and the inline script, style and handlers are each
pinned by a fact in `AgendaConversionTests` or `EventsConversionGuardTests`; the checked fill is the foundation's gold.

## Deviations from Plan

**1. [Rule 1 - Bug] Filter dropdown help sentence 1.39:1**
- **Found during:** Task 3 sweep. The Bootstrap menu is white and its text parchment (the foundation themes only the
  navigation dropdown). Fix: `.agenda-filter-menu` sets the overlay surface, parchment text and muted tone through the
  `--bs-dropdown-*` variables in the pages layer. Re-swept: 0 residual. Included in 57711283.

**2. Task 3 had no separate commit.** The sweep produced one source change (above), committed with Task 2 because it
lives in `agenda.css`, which Task 2 created.

**3. Tooling (outside the repository):** added `tools/agenda-switch-probe.mjs` and `tools/agenda-rows-probe.mjs` under
`~/qb-ui-review/phase-97/tools`.

**4. Commit trailer** uses `Claude Sonnet 5.5` (the model that ran), where the brief named Opus 5.5.

## Known Stubs

None.

## Threat Flags

None. The dialog form keeps its antiforgery token and posts to the unchanged `SelectGroup`; `agenda.js` writes with
`.value` and `textContent` only (guarded by `AgendaModule_WritesNoMarkupFromStrings`); no controller, filter or gate
changed, and `AgendaTenantIsolationTests` passes unchanged.

## Self-Check: PASSED

Files found: `agenda.js`, `AgendaConversionTests.cs`, both views and sheets; commits 6b5ede7f and 57711283 present;
`grep -cE "style=|onclick|stopPropagation"` is 0 for both views; `agenda.css` is not in `_Layout.cshtml`.
