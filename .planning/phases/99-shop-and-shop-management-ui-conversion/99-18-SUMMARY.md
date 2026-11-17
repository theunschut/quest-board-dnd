---
phase: 99-shop-and-shop-management-ui-conversion
plan: 18
subsystem: shop-ui
tags: [shop, css-cleanup, guards, legacy-pages, definition-of-converted]
requires: ["99-17"]
provides:
  - shop.css retired: out of the global list and deleted, proven harmless to every other page
  - legacy-pages.css trimmed to its one shared wide-container rule
  - final ShopConversionGuardTests facts (scope, theme, motion, sheet ownership, no shop selector elsewhere, planning-reference coverage)
  - counts-after.json with every area count at zero beside the ROADMAP figures
affects: [99-19, 99-20, 99-21]
tech-stack:
  added: []
  patterns: [computed-style comparison with and without a sheet to prove a deletion harmless, guard facts that read the sheets and views from disk so a new shop file cannot escape them]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/base.css
    - .claude/ui-guidelines.md
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/shop.css
decisions:
  - "shop.css held only four filter rules that no view, script or source file renders; it was deleted after a computed-style comparison on seven non-shop pages showed no difference."
  - "The sheet-ownership guard treats a short list of shared names (Bootstrap, the icon font, foundation components) as allowed in other sheets and everything else the three shop sheets define as the shop's alone."
  - "The one shop name found in a foundation sheet (the merchant in base.css's reduced-motion hover list) was removed, because the merchant is a button the list already covers."
  - "legacy-pages.css stays linked and unlayered; its comment names the group picker layout only, because the Dungeon Master profile no longer renders the container class."
metrics:
  duration: "about 20 minutes of wall clock (sequential single pass)"
  completed: 2026-11-16
status: complete
actuals:
  tokens: 6300
  tasks: 3
  commits: 2
---

# Phase 99 Plan 18: Cleanup and guards Summary

The last old shop sheet is gone, `legacy-pages.css` holds only the one rule other layouts still share, every area count is zero against the ROADMAP figures, and the area guards are in their final shape with no test lost and nothing outside the area changed.

## What changed

- **shop.css retired (tracer).** `~/css/shop.css` left the global list of `_Layout.cshtml` and the file is deleted. It held four rules (`.filter-label`, `.filter-check-label` with its checkbox rule, `.filter-apply-btn`, `.filter-clear-btn`) that the converted shop filter card no longer uses. `selector-scan.py` reports all four as unused: no view, partial, script or source file renders them anywhere. Planning's claim that Contacts/Index renders them was a substring match; Contacts uses `contact-filter-*`, a different set of classes.
- **legacy-pages.css.** The file already held only `.container.mt-4 { max-width: none }` after 99-16; the header comment now says why it remains. It stays unlayered and linked by the desktop, group picker and platform layouts. The comment names the group picker layout only: `_Layout.GroupPicker.cshtml` is the one view that renders `container mt-4` (the plan text also names the Dungeon Master profile pages; a profile test asserts they no longer render it).
- **base.css.** The merchant's class name was in the foundation reduced-motion hover list. It is removed; the merchant is a `button`, which the same list already covers, so nothing changes. It was the only shop name any other sheet carried.
- **ui-guidelines.md.** "Themes and tokens" now says a page opts in through `ViewData["Theme"]` and both layouts emit it as `data-theme` on `<html>`. The "Legacy page sheets" bullet no longer lists the shop sheets as unlayered.

## Outside-shop measurement (Task 1)

`after/99-18/outside-shop.json`, read-only GETs as p99dm on the "P99 Audit Shop" board, desktop 1440x900: every element's computed style compared with `shop.css` served and with its request answered by an empty sheet.

| Page | Elements | Elements carrying the four classes | Differences |
|---|---|---|---|
| /Contacts | 100 | 0 | 0 |
| / (quest board) | 82 | 0 | 0 |
| /Calendar | 318 | 0 | 0 |
| /Characters | 107 | 0 | 0 |
| /Players | 117 | 0 | 0 |
| /Agenda | 235 | 0 | 0 |
| /Events | 121 | 0 | 0 |

(A first run that included /Shop showed one `transform` difference of 0.12px on the merchant button: the capped bob caught at two moments. /Shop is the area itself, so it was left out of the outside-shop list.)

**The two dialogs, as measured.**

- `#addCharacterModal`: rendered by no view, partial or script (grep of every view; `/Shop`, `/ShopManagement` and `/ShopManagement/Create` fetched on both layouts: not in the markup). Its alert and required-mark rules were deleted with the file; nothing is moved.
- `#denyModal`: rendered by `_ShopManagementDialogs.cshtml` once on `/ShopManagement` on both layouts. Its rules were moved earlier; it uses the foundation `alert-warning`, `form-text` and the `text-danger` required marker with its hidden "(required)" word, and no shop sheet holds an alert or required-mark rule or a literal colour.

**The site.css duplicate.** `site.css` was renamed `legacy-pages.css` in an earlier phase and no longer exists; it is now in `RetiredFiles`. Its shop rules left through the trims in 99-04 to 99-16 (the return and sell buttons in 99-16); `LegacyPagesCss_HoldsOnlyTheSharedWideContainerRule` and `NoSheetOutsideTheShopSheets_HoldsAShopSelector` pin the result.

## Counts (counts-after.json)

`tools/counts-99.py` (new) reproduces the ROADMAP figures exactly from the phase start: the eleven views plus the six old shop sheets plus `legacy-pages.css`, all as at BASELINE_SHA (the shop rules that lived in `legacy-pages.css` were 72 hex, 72 rgb, 49 important and 6 endless animations).

| Count | ROADMAP | Measured before | After |
|---|---|---|---|
| hex colours | 189 | 189 | 0 |
| rgb() | 209 | 209 | 0 |
| important flags | 152 | 152 | 0 |
| style attributes | 15 | 15 | 0 |
| style blocks | 4 | 4 | 0 |
| inline scripts | 9 | 9 | 0 |
| on-event attributes | 40 | 40 | 0 |
| endless-animation declarations | 16 (10 distinct animations, which the ROADMAP calls "ten") | 16 | 0 |

The only animation left is the merchant's bob: one `@keyframes`, transform only, three runs, inside the no-preference query, none on the phone layout. `ShopSheets_MotionIsFinalAcrossEveryShopSheet` proves it with its own self-test.

## Guards (ShopConversionGuardTests)

New facts, each with a self-test where it is a scanner: `Layouts_LinkNoShopSheet` (+ `LayoutScanner_...`), `PhaseScope_IsFullyConverted` (every Views/Shop, Views/ShopManagement and Shared/_Shop* view is in `ConvertedViews` and the list names nothing else; the three small chip and badge partials were added to the list) (+ `ScopeScanner_...`), `PhaseSourceFiles_ListEveryShopSourceFile`, `ShopViews_AllSetTheShopTheme` over all ten pages (+ `ThemeScanner_...`), `ShopSheets_MotionIsFinalAcrossEveryShopSheet` (+ `InfiniteMotionScanner_...`), `LegacyPagesCss_HoldsOnlyTheSharedWideContainerRule` (+ `SheetRuleScanner_...`), `LegacyPagesCss_HoldsNoShopSelector`, `NoSheetOutsideTheShopSheets_HoldsAShopSelector` (+ `ShopOwnershipScanner_...`). `RetiredFiles_AreGone` now also lists `shop.css` and `site.css`. The existing theme seam, token scanner, whole-image scanner, module scan, raw-date scan and planning-reference scan stand; `PhaseSourceFiles` gained the shop controller tests, `ShopIndexViewModel.cs` and `base.css`. The planning-reference check over every line the phase added to Service, IntegrationTests and UnitTests prints `ids-ok` (one seed string in the guard's own self-test held a whole plan-id shape and was split into fragments).

Red then green: `ShopIndex_Desktop_LinksTheShopPageSheetAndNoOldShopSheet` and `NonShopDesktopPage_LinksNoShopSheet` failed before the layout link was removed; the ownership guard failed on `base.css: .shop-merchant` when that name was put back and passed after it was removed again. The other new guard facts passed on first run because the earlier slices had already brought the tree to their state; their scanners are pinned by the self-tests.

## Test inventory

| Project | Before (list lines) | After (list lines) | Names missing from before |
|---|---|---|---|
| QuestBoard.UnitTests | 1406 | 1413 | 0 (two theory rows with embedded line breaks list differently; present in both inventories) |
| QuestBoard.IntegrationTests | 2322 | 2571 | 1: `MobileShopIndex_MobileUserAgent_OmitsPurchaseHistoryPanel`, renamed in 99-17 and listed in `tests-renamed.md` |

Two rows were added to `tests-renamed.md` for this plan (the old-sheet guards now read `legacy-pages.css` only; the legacy sheet comment). No test was deleted.

## Untouched paths

`git diff` from BASELINE_SHA over the controllers, Domain, Repository, `HtmlHelperExtensions.cs`, `AmbientClockSeamTests.cs`, `appsettings.json`, `tokens.css`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` is empty (`counts-after.json` `untouched.changed` is `[]`). Nothing writes Events or Quests.

## Regression sweep against BEFORE

Port 8320, 38 manifest rows, both User-Agents at every named width: 140 records, all 200, no console error, page error, failed action or failed assert, no horizontal overflow. `readability-compare.py` against BEFORE with the merged allow file: 129 compared, 0 failing; with `--zero-contrast phase`: 0 residual contrast failures. The 4 POSTs aborted at the network layer are the sweep's own aborted form submissions.

## Tests

- Unit tests 1424 passed. Integration tests 2560 passed, 16 skipped (SQL Server migrator tests), 2576 total (2544 passed before; +16).

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate.** Auto mode is off and a sequential executor cannot be resumed after a checkpoint, so the tracer (shop.css retired) was committed, proven by its tests and the computed-style comparison, and the rest followed; the full sweep at the end re-proved the shop and every other page.

**2. [Rule 1 - Bug] `readability-compare.py --pages phase` compares nothing.** The tool reads `--pages` as a list of row ids, so "phase" matched none (compared 0). The comparison was run without `--pages` (129 compared). The first full run flagged `mgmt-create-errors` (desktop, 1440 and 320): the Preview control's key changed because the button gained the id `previewButton` when Create got its Preview dialog, a rename an earlier slice allowed for `mgmt-create` only. The same rename was added to the merged allow file for the errors row; nothing in this plan caused it.

**3. [Operator-set rule honoured] `fixtures.mjs --fresh-purchases` was not run.** The permission classifier refused the command (it can make purchases). It would have made none (20 purchases younger than 20 hours), and the standing rule is that this plan makes no purchase; the sweep used the existing `fixtures.json`. No purchase, Return or Sell was made, and none of the 18 day-0 purchases was touched.

**4. [Rule 2 - Missing] The sheet-ownership guard exempts a list of shared names.** The plan says it fails when any other sheet defines a rule for a class the three shop sheets define; taken literally that fails on `btn`, `badge`, `form-control` and the other Bootstrap and foundation names the shop sheets restyle. The guard exempts a short, visible list of those names and holds everything else the shop sheets define to the rule, which is what found the merchant name in `base.css`.

**5. The plan's claim that the Dungeon Master profile pages use the wide-container rule is stale.** Only the group picker layout renders it, so the comment says so; the rule and the file stay as the plan requires.

**6. `counts.py` is the previous phase's tool.** It counts another area's views. `counts-99.py` was written for this area (outside the repository, in the scratch tools) and writes the `counts-after.json` shape the plan names.

## Known Stubs

None.

## Threat Flags

None new. T-99-47 mitigated (the computed-style comparison on seven non-shop pages: 0 differences, plus selector-scan showing no consumer). T-99-48 mitigated (inventory compared, the one missing name is in `tests-renamed.md`). T-99-49 mitigated (untouched-paths diff empty).

## Self-Check: PASSED

Present: `legacy-pages.css` (one rule), `ShopConversionGuardTests.cs`, `outside-shop.json`, `counts-after.json`, `tests-after.txt`, `after/99-18/report.json` and `allow.json`; `shop.css` absent. Commits found: 924d60c5, be79fc56. Both suites green; port 8320 stopped, 8321 left running; no background process started by this plan is running.
