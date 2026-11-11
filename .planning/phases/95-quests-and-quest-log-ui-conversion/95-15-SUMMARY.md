---
phase: 95-quests-and-quest-log-ui-conversion
plan: 15
subsystem: ui-move
tags: [move, foundation, pages-layer, view-merge, shared-component, contrast]
requires:
  - phase: 95-14
    provides: Quest Log conversion, merge pattern (one view, phone tweaks under .mobile-layout), per-User-Agent readability compare
  - phase: 95-03
    provides: Gate 1 merge answers (Move/Index = yes)
provides:
  - .move-control-reason as a shared component rule in components.css
  - Move/Index as one view serving both layouts (phone twin and its sheet deleted)
  - move-entry.css as an @layer pages sheet on tokens only, linked from the view instead of the desktop layout
  - zero text-contrast failures on both Move states under both User-Agents, no exception entry
affects: [closure plan: mobile.css now holds only the container-fluid padding rule]
tech-stack:
  added: []
  patterns: [one view for both layouts with the Bootstrap grid stacking and phone tweaks under .mobile-layout, entry-summary heading as an h2 in the card body so the display step stays on the page title, light status variants for the consequence stripes]
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/wwwroot/css/move-entry.css
    - QuestBoard.Service/wwwroot/css/mobile.css
    - QuestBoard.Service/Views/Move/Index.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.IntegrationTests/Tests/BoardMoveViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveEventTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMovePageTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveEndToEndTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  deleted:
    - QuestBoard.Service/Views/Move/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/move-entry.mobile.css
key-decisions:
  - "Merge branch taken: Move/Index merged into one view (Gate 1 item 8 = yes, 95-03 SUMMARY Merge answers). No new checkpoint."
  - "The entry summary heading (This Quest / This Event) is an h2 inside the card body, not the card header: .modern-card-header h2 takes the 28px display step, which is reserved for the page title (the h1 in the move card), the same call as 95-14."
  - "The consequence stripes use the light status variants (--qb-*-text), which hold their contrast on glass, instead of the old Bootstrap hex colours."
  - "The hint text takes the card colour (size only distinguishes it) rather than the muted token, which measures 4.25:1 on one glass card."
requirements-completed: [D-16, D-21, D-24, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8]
duration: ~1 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 18700
  tasks: 2
  commits: 2
---

# Phase 95 Plan 15: Move reason line promoted, Move page merged Summary

The disabled-Move reason line is one shared component rule, and the Move page is one view for both layouts with the "This Quest" / "This Event" heading on the phone too, its rules in an `@layer pages` sheet on tokens only, and every board-move guarantee intact.

## What changed

**Task 1 (`4aeb4626`), shared reason line.**
- `components.css` gains `.move-control-reason { color: var(--qb-fg-muted); font-size: var(--qb-text-sm); }` inside the components layer with a why-comment. The rule left `move-entry.css` (`.move-hint` stayed) and left `mobile.css`, which held its only hex literal (`#D9CDA8`). Markup and the test that pins it (`BoardMoveControlTests`) are unchanged.
- `mobile.css` remaining rules: the `.container-fluid` phone padding only.
- Foreign-page check: `fx-events-details` compared against BEFORE, both User-Agents, 4 records, 0 problems. `details-dm` and `manage-open` swept too; their `controlsLost` (the Delete Quest link that became a button in 95-13) and the Quest Details calendar header contrast are the earlier plans' recorded changes and listed exceptions, not this plan's.

**Task 2 (`2fb483ab`), Move merge.**
- One `Move/Index.cshtml` wrapped in `.move-page`: entry summary `.modern-card` in `col-lg-4` with the `h2` `This Quest` / `This Event`, then `col-lg-8` with the page header `h1`, the changed-since-preview `.alert-warning`, the board select with `btn-info` Show What Will Happen, the preview `section` with its `h2` `What will happen on {board}`, the consequence list, `<hr>` and the action row (`move-actions`: Back `btn-secondary` left, the move confirm `btn-warning` with `fa-right-left` right). Copy, wall-clock dates, the disabled-control markup contract, SuperAdmin-only access and the confirm form (id, targetBoardId, stamp, `data-disable-on-submit`) are unchanged. `@section Styles` links `~/css/move-entry.css`. No style attribute, script element, span or trap class.
- `move-entry.css` is one `@layer pages` block, tokens only, no important flag, no media query. Phone tweaks sit under `.mobile-layout`: outer row gutter zeroed, tighter card body, the two actions share the row at the 44px target.
- The desktop layout no longer links `move-entry.css` globally (`_Layout.cshtml`, one line); the view loads it through its Styles section, so both layouts get it only on this page. `_Layout.cshtml` is not in the plan's file list; without the removal the sheet would still be global.
- Deleted `Index.Mobile.cshtml` and `move-entry.mobile.css`; a repo-wide search finds no other reference.
- Guards: Move view in `ConvertedViews` and `ViewSheetLinks`, sheet in `ConvertedSheets` and the planning-reference scan list; new fact `MoveMergedView_LeavesNoPhoneTwinOrPhoneSheet` fails if either retired file returns.

## Tests rewritten (old intent, new assertion); none deleted

| Test | Old intent | New assertion |
|------|-----------|---------------|
| `BoardMoveViewsTests.TheDesktopLayoutLinksTheDesktopSheetOnly_AndTheMobileLayoutTheMobileSheetOnly` (renamed `BothLayoutsLinkTheOneMoveSheetAndNoPhoneOnlySheet_AndThePhoneAnswerCarriesTheMobileLayoutBodyClass`) | each layout links its own sheet only; phone shows `move-card-mobile` | both layouts link `/css/move-entry.css` and never `move-entry.mobile.css`; desktop has no `mobile-layout`; the phone answer has the `mobile-layout` body class and `move-page` |
| `BoardMoveViewsTests.TheMoveViews_KeepScriptInlineStyleAndTheTrapClassesOut` | no inline code or trap class in both Move views | same scan, the one remaining view path (deleted path dropped from InlineData) |
| `BoardMoveViewsTests.TheMoveStylesheets_HaveNoMediaQuery` | no media query in both Move sheets | same scan, the one remaining sheet |
| `BoardMoveEventTests` desktop fact | desktop has no `move-card-mobile` | desktop has no `mobile-layout` |
| `BoardMoveEventTests.Page_OnMobile_ShowsTheEventCopyInTheMobileCards` (renamed `...InThePhoneLayout`) | phone shows the event copy in the phone cards | phone has `mobile-layout` and `move-page`, plus the same event copy assertions |
| `BoardMovePageTests.AMobileUserAgent_GetsTheMobileCardsWithTheSameSentences` (renamed `...GetsThePhoneLayoutWithTheSameSentences`) | phone cards and the `d-flex gap-2` action row | `mobile-layout`, `move-page` and the `move-actions` action row; every sentence assertion unchanged |
| `BoardMoveEndToEndTests` (quest walk, both User-Agents) | phone page has `move-card-mobile`, desktop does not | phone has `mobile-layout` and `move-page`, desktop has no `mobile-layout` |
| `BoardMoveStaticGuardTests` | presence list and `FeatureSourceFiles_ReachTheMoveFeature` named the phone view | phone view and phone sheet removed from the list; the reach fact now names `Move/Index.cshtml` |
| `BoardPageStaticGuardTests` | listed the phone view | phone view removed from the list |
| `AmbientClockSeamTests.GuardedRelativePaths` | listed both Move views | only `Move/Index.Mobile.cshtml` removed; desktop `Move/Index.cshtml` and every other entry stay |

## Contrast and sweep

Sweep `after/95-15`: `move-quest` and `move-event`, SuperAdmin, preview shown, desktop 1440 and 320, phone 412 and 390: 8 records, all 200, no horizontal overflow, no small target, no missing focus ring, no console error, asserts passed.
`readability-compare --zero-contrast move-quest,move-event --exceptions contrast-exceptions.json`: 8 compared, 0 failing rows, 0 residual contrast failures (the action buttons that failed inside the card BEFORE among them), 0 poster-glyph violations, `ok`. The 95-15 `allow.json` is `{}`: no relabel or merged-away marker needed an allowance. `contrast-exceptions.json` was not touched (12 entries, as left by 95-14).
Frames: `~/qb-ui-review/phase-95/after/95-15/{desktop,mobile}/`.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1311 passed (one case fewer: the deleted view's `AmbientClockSeamTests` entry), integration 1791 passed, 11 skipped (pre-existing), 0 failed. Counts differ from 95-14 only by the removed theory cases (4) and the added merge-guard cases (2). Task filter `BoardMove|BoardPageStaticGuardTests|QuestsConversionGuardTests|UiFoundation`: 388 passed. Task 1 filter (`BoardMoveControlTests|UiFoundation`): 139 passed.
- Working-tree app restarted on `http://localhost:8000` on this build; port 8001 untouched.
- No move or other write was submitted: the sweep only runs the preview GET.

## Deviations from Plan

**1. [Rule 3 - Blocking] Desktop layout still linked the Move sheet globally.** `_Layout.cshtml` loaded `move-entry.css` on every desktop page; removing it was needed for the sheet to leave the global list as the plan states. One line removed; the view's Styles section links it.

**2. [Judgement] Summary heading in the card body.** The plan says `h2` for `This Quest`; a card-header h2 would take the 28px display step, so the summary card has no header and the h2 sits in the body (20px), as in 95-14.

**3. [Judgement] Consequence stripes and hints re-toned.** The Bootstrap hex stripes became the light status variants (needed: no literals in a layered sheet), and `.move-hint` inherits the card colour rather than a muted tone.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-36: controller authorisation and the preview stamp untouched; `BoardMoveAuthorizationTests` and the end-to-end walks pass. T-95-37: the only removal from the closed ambient-clock list is the deleted phone view, in the same commit as its deletion.

## Self-Check: PASSED

Verified: `components.css` contains `.move-control-reason`; `move-entry.css` opens `@layer pages` and holds no reason rule; `Move/Index.cshtml` contains `This Quest`, `@section Styles` and `move-entry.css`; `Index.Mobile.cshtml` and `move-entry.mobile.css` are gone; commits `4aeb4626` and `2fb483ab` exist; full build and test green; `readability-compare` with zero-contrast over both Move rows printed `ok`.
