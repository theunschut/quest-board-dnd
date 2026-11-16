---
phase: 99-shop-and-shop-management-ui-conversion
plan: 09
subsystem: shop-ui
tags: [shop, status-vocabulary, rarity-chip, type-chip, guards, razor]
requires: ["99-08"]
provides:
  - _ShopStatusBadge partial, the one status badge markup (fill, icon, word from ShopItemDisplay)
  - _ShopTypeChip partial, one neutral badge with the type icon and word
  - ShopItemViewModel.TypeDisplayName; StatusDisplayName and RarityDisplayName delegate to ShopItemDisplay
  - management list (both layouts) on the shared rarity chip, status badge and type chip; both Edit status alerts on the status badge
  - old rarity, rarity-text, legendary glow and type-* rules deleted from shop.css and legacy-pages.css
  - guards for the status mapping and for endless motion in the new shop sheets
affects: [99-10, 99-11, 99-12]
tech-stack:
  added: []
  patterns: [one static mapping read by partials and view model, scanners with self-tests as guards]
key-files:
  created:
    - QuestBoard.Service/Views/Shared/_ShopStatusBadge.cshtml
    - QuestBoard.Service/Views/Shared/_ShopTypeChip.cshtml
    - QuestBoard.IntegrationTests/Tests/ShopItemDisplayTests.cs
  modified:
    - QuestBoard.Service/ViewModels/ShopViewModels/ShopItemViewModel.cs
    - QuestBoard.Service/Views/ShopManagement/Index.cshtml
    - QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml
    - QuestBoard.Service/Views/ShopManagement/Edit.cshtml
    - QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/shop.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/shop-management-index.mobile.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
decisions:
  - "The management list's status cells, the phone badge and both Edit alerts all render the shared badge; the Edit views' Replace and badge switch, the desktop switches and the phone switch are gone"
  - "Types are one neutral bg-secondary badge with icon and word, so the rarity chip is the only coloured chip"
  - "The ItemStatus, ItemType and ItemRarity enums, controllers, services and repositories are untouched; the details partial is untouched"
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 4
---

# Phase 99 Plan 09: Status and Type Vocabulary Summary

The management list now shows the shared rarity chip, one status vocabulary (Draft reads Pending Review, Published reads Available) and the type words on both layouts, and both Edit status alerts use the same status badge, all from one mapping and three partials, with the old competing rules deleted.

## What changed

- **Partials.** `_ShopStatusBadge` (model `ItemStatus`) renders `badge` plus `ShopItemDisplay.StatusBadgeClass`, an aria-hidden `StatusIcon` and `StatusName`: Pending Review (bg-warning, fa-hourglass-half), Available (bg-success, fa-check-circle), Archived (bg-secondary, fa-box-archive), Denied (bg-danger, fa-ban). `_ShopTypeChip` (model `ItemType`) renders a `badge bg-secondary` with `TypeIcon` and `TypeName` (Equipment, Magic Item, Quest Item).
- **View model.** `StatusDisplayName` and `RarityDisplayName` delegate to `ShopItemDisplay`; `TypeDisplayName` is new. Nothing else in the code read the old names.
- **Sites converted.** Desktop management: three status cells (review, my items, other items), three type cells, three rarity cells. Phone list: rarity chip and status badge (its own switch, which said Active, is gone). Both Edit status alerts: the `.Replace("Published", "Available")` and badge switch are gone. `shop-pages.css` is linked from both management index views.
- **Rules deleted.** `shop.css`: 154 lines (the item-rarity rule, five per-rarity rules, the legendary glow keyframes, the three type-* rules and the five keyframes that only they used). `legacy-pages.css`: 156 lines (same set and their orphaned comments). `shop-management-index.mobile.css`: the `.shop-mgmt-item-card .badge` text-shadow rule (the legacy-collision tool listed it; no ancestor sets a shadow on a badge).
- **Guards.** `ShopViews_UseTheSharedStatusMapping` (no `Replace("Published"`, no status switch, no icon built from the status name in any of the eleven shop files) and `ShopSheets_NewSheetsHoldNoEndlessAnimationOrTransitionAll` (over shop-pages.css now, shop-pages.mobile.css and shop-form.css when present), each with a self-test on seeded rules. `ShopSheets_HoldNoRarityRuleOutsideTheChipSheet` replaces the narrowing guard. `PhaseSourceFiles` gained every file this plan touched.

## Tests renamed

Recorded in `~/qb-ui-review/phase-99/tests-renamed.md`:
- `ShopManagementList_KeepsItsOwnChipMarkupForNow` became `ShopManagementList_BothLayouts_ShowTheSharedRarityChip`.
- `ShopSheets_OldRarityRulesNeverReachANewChip` became `ShopSheets_HoldNoRarityRuleOutsideTheChipSheet`.

New facts: `ShopItemDisplayTests` (rarity, status, type mapping and view model delegation), management status vocabulary on both layouts, the type words on desktop, and the Edit status alert for Draft, Published, Archived and Denied on both layouts.

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2380 passed, 0 failed, 16 skipped (SQL Server migrator tests), total 2396 (2366 before; +30 new facts).
- Sweep on 8320 of `mgmt-index`, `mgmt-index-empty`, `mgmt-edit-draft`, `mgmt-edit-published`, `mgmt-edit-denied` under both User-Agents (20 captures, all 200): `readability-compare.py` against BEFORE reports 0 failing rows, `ok`. The one accessible-name change (the phone Edit page's main landmark name now reads Pending Review instead of Draft) is recorded as a rename in `after/99-09/allow.json`.
- `legacy-collision.py` on both management index views lists no rarity, type or badge rule.
- No file under Domain, Repository or Controllers changed (scope check printed scope-ok); the details partial is unchanged. Nothing was posted and no purchase was made.

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate met by an automated probe**
- The run is not in auto mode and a sequential executor cannot be resumed after a checkpoint, so as in the previous slices the committed tracer was swept on 8320 under both User-Agents (`after/99-09/tracer/`) and the screenshots read before the second task: chips, Pending Review and Available badges render correctly on both layouts.

**2. [Rule 1 - Bug] Old phone badge rule still styled the new badges**
- **Found during:** Task 3, `legacy-collision.py`. **Fix:** deleted the rule in `shop-management-index.mobile.css` (not in the plan's file list); the sweep stays clean. **Commit:** c2522322.

## Known Stubs

None.

## Threat Flags

None. T-99-22 is mitigated (one mapping and one partial at all six sites, pinned by `ShopViews_UseTheSharedStatusMapping`); T-99-57 is mitigated (the partials take enum values and render constant words through Razor encoding).

## Self-Check: PASSED

Present: `_ShopStatusBadge.cshtml`, `_ShopTypeChip.cshtml`, `ShopItemDisplayTests.cs`, `after/99-09/` (report.json, allow.json, tracer, PLAN_START). Commits found: 888470d7, f6ad99c1, c2522322. Both test projects green; port 8320 stopped, 8321 left running.
