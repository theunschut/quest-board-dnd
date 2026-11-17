---
phase: 99-shop-and-shop-management-ui-conversion
plan: 14
subsystem: shop-ui
tags: [shop, management, dashboard, deny-dialog, bulk-actions, es-module, guards]
requires: ["99-13"]
provides:
  - shop-management.js (Deny dialog loader and Bulk Actions notice, from data attributes)
  - ShopManagement/Index on both layouts converted: aside sections with scrolling tables on desktop, slips on the phone, one status vocabulary, filled named row actions
  - _ShopManagementDialogs partial (Deny and Bulk dialogs on the opaque foundation modal, shared by both layouts) and _ShopManagementStockBadge partial
  - the twelve management confirmations on confirm-submit.js with today's sentences
  - the last management phone sheet (shop-management-index.mobile.css) retired
affects: [99-15, 99-16, 99-17]
tech-stack:
  added: []
  patterns: [dialog filled from the opener's data attributes with textContent and a server-rendered action, shared dialog partial for twin views, aside cards with table-responsive tables]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/shop-management.js
    - QuestBoard.Service/Views/Shared/_ShopManagementDialogs.cshtml
    - QuestBoard.Service/Views/Shared/_ShopManagementStockBadge.cshtml
  modified:
    - QuestBoard.Service/Views/ShopManagement/Index.cshtml
    - QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/shop-pages.css
    - QuestBoard.Service/wwwroot/css/shop-pages.mobile.css
    - QuestBoard.Service/wwwroot/css/shop.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/shop-management-index.mobile.css
decisions:
  - "The Deny and Bulk dialogs are one shared partial rendered by both layouts instead of two copies, so the twins cannot drift."
  - "Phone slips show the Denial Reason block as the approved drawing does; the phone keeps today's actions (no Edit & Resubmit link, which duplicates Edit)."
  - "Table header stays the foundation wood header (Gate 1 look check: wood), so no page rule was added for it."
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 99 Plan 14: Management List Summary

Both layouts of the shop management dashboard now run on the foundation in the navy theme: every confirmation asks today's question through `confirm-submit.js`, the Deny and Bulk Actions dialogs are driven by one small module from data attributes, and the lists use one status vocabulary, filled named buttons and tables that scroll inside their cards.

## What changed

- **Behaviour.** The twelve inline confirm handlers (eight desktop, four phone) became `data-confirm-message` on their forms, handled by the unchanged `confirm-submit.js`. `shop-management.js` is a small ES module: on `#denyModal`'s `show.bs.modal` it reads `data-deny-name` and `data-deny-url` from the opening button, writes the name with `textContent`, sets the form action and clears the reason; each `[data-bulk-action]` button shows today's native alert built from its action word. The views' inline scripts are gone.
- **Desktop.** The page-title card holds the h1 and the header actions (Add New Item `btn-primary`, View Shop and Bulk Actions `btn-secondary`). Three `modern-card--aside` sections (Items Awaiting Review, My Items, All Other Published Items) with h2 titles and their existing count subtitles, each a foundation `.table` inside `.table-responsive`. Stock badges come from one partial; status cells use `_ShopStatusBadge`; the `table-warning` and `table-danger` row tints are gone. The denial reason is a block with a 4px danger stripe and ink text (words unchanged, date through `Html.LocalTime`). Row actions are filled by role (View `btn-info`, Edit and Edit & Resubmit `btn-warning`, Publish and Reopen `btn-success`, Archive `btn-secondary`, Deny and Delete `btn-danger`), icon-only with an `aria-label` and the old `title`, 31 to 34px square on desktop.
- **Phone.** A visually hidden h1 (Gate 1 item 13: hidden), the same header actions at full width, then one merged list of slips (h2 name, rarity chip, price, status badge, denial block, 44px action row). The empty state now reads No Items Yet with the desktop sentence and Create First Item as `btn-secondary` (Gate 1 item 13: secondary).
- **Dialogs.** One partial, rendered by both layouts: Bulk Actions (Gate 1 item 7: keep; the three buttons, the note line and Close kept; the phone has no trigger and keeps the restyled dialog) and Deny Item. Both sit on the foundation opaque modal: h2 titles, named Close buttons, no `bg-dark`, no `btn-close-white`. The Deny dialog's dismiss button stays Cancel (Gate 1 item 22: no rename) and Deny is `btn-danger`.
- **Rules deleted.** `shop.css`: the whole old deny-dialog block (11 `#denyModal` rules with literal colours and `!important`). `legacy-pages.css`: `.btn-sm.btn-action`. The sheet `shop-management-index.mobile.css` (66 lines, glass cards, `!important` colours) is deleted. The dashboard rules live in `shop-pages.css` (`.mgmt-*`) and `shop-pages.mobile.css` (full-width header actions, 44px row actions).
- **Deny dialog alert and required mark moved, not dropped.** The old rules forced the warning alert transparent with a literal amber border and white text, and the required star a literal red. The alert is now the foundation `.alert.alert-warning` (overlay panel, amber-light text variable, status stripe) and the star is the foundation `text-danger` marker with the hidden word "(required)".

## Confirm sentences (byte check against the frozen baseline, p99dm, native confirm text with every POST aborted)

| UA | Action | Sentence | Equals BEFORE |
|----|--------|----------|---------------|
| desktop | Publish | Are you sure you want to publish this item? | yes |
| desktop | Archive | Are you sure you want to archive this item? | yes |
| desktop | Reopen | Are you sure you want to reopen this item? | yes |
| desktop | Delete | Are you sure you want to permanently delete this item? This action cannot be undone. | yes |
| phone | Archive | (same as desktop) | yes |
| phone | Reopen | (same as desktop) | yes |
| phone | Delete | (same as desktop) | yes |

Publish is not offered on the phone to a Dungeon Master (administrators only), before and after, so it has no phone row. The three bulk notices (discontinue, activate, restock) read "Bulk {action} functionality will be available in a future update." and are identical before and after.

## Deny dialog alert, before and after (lightest painted pixel inside the alert, its stripe and border left out)

| | Background | Text colour | Contrast |
|---|---|---|---|
| BEFORE (8321), desktop | transparent over the dialog's `rgb(47, 27, 14)` | `rgb(255, 255, 255)` | 16.36:1 |
| AFTER (8320), desktop | `rgb(18, 22, 43)` | `rgb(255, 191, 94)` | 10.95:1 |
| AFTER (8320), phone | `rgb(18, 22, 43)` | `rgb(255, 191, 94)` | 10.95:1 |

The baseline was already readable (the old rules forced it); the foundation alert keeps it well above 4.5:1 and drops the literal colours.

## Gate 1 answers applied

- Item 7 (keep): Bulk Actions kept and restyled on desktop; the phone dialog kept, no phone trigger.
- Item 13 (hidden, secondary): phone h1 visually hidden; Create First Item is `btn-secondary` so Add New Item stays the one `btn-primary`.
- Item 17 (keep): section title Items Awaiting Review.
- Item 22 (no): Deny dialog dismiss label stays Cancel; title and Deny button wording unchanged.
- Item 6 (wood): the table header is the foundation wood header.
- Item 16 (amber): legendary chip is the existing amber chip from the shared sheet.

## Tests renamed

Recorded in `~/qb-ui-review/phase-99/tests-renamed.md`: `MobileViewsTests.GetMobilePage_ShopManagementIndex_ReturnsSuccessAndMobileLayout` (old glass card class and `shop-management-index.mobile.css`) now asserts `mobile-layout`, the header actions, the list or the empty state, both shop sheets and that the old phone sheet is gone.

New facts: 12 methods (21 cases with the layout theories) in `ShopRenderTests` (confirm sentences by data attribute, deny trigger attributes and token, modules and no inline code, bulk binding, three aside sections, header roles, row action roles and names, denial block and local-time date, phone slips, empty state on both layouts, deny dialog foundation shape, theme and sheet links), `KeyboardScriptGuardTests.ShopManagementModule_LoadsTheDenyDialogFromItsTrigger`, and guard-list growth (`ShopModules`, `ConvertedViews`, `ViewSheetLinks`, `RetiredFiles`, `PhaseSourceFiles`, `ShopViews` for the two partials).

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2482 passed, 0 failed, 16 skipped (SQL Server migrator tests), total 2498 (2461 before; +21 facts).
- `mgmt-probe.mjs` on 8321 and 8320 (`after/99-14/mgmt-before.json`, `mgmt.json`): every recorded confirm sentence equals BEFORE's byte for byte; the deny dialog shows the name and posts to `/ShopManagement/Deny/{id}` from the module on both layouts; no POST was sent; no page errors. Focus: the first row action shows the two-tone ring and its accessible name is now "View item" (it was the bare icon glyph before).
- Sweep on 8320 (`mgmt-index`, `mgmt-index-empty`, `mgmt-deny-dialog`, `mgmt-bulk-dialog`, both User-Agents, 14 captures, all 200): no overflow at 320 desktop or 390 phone (tables scroll inside their cards), no console or page error, no small targets, no missing focus ring, 0 contrast failures after one fix (the empty-section sentence now takes the card colour). `readability-compare.py` against BEFORE with `after/99-14/allow.json` reports `ok`; the allow entries are the accessible-name renames (aria-labels on the icon-only actions, `type="button"` on the dialog buttons, dialogs named by their h2 titles, hidden "(required)" word).
- Scope: `git diff` of `QuestBoard.Service/Controllers` and `confirm-submit.js` against the plan start is empty.
- `legacy-collision.py` lists nothing for either view.

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate.** Auto mode is off and a sequential executor cannot be resumed after a checkpoint, so as in earlier slices the tracer was committed and then proven in a real browser (`after/99-14/tracer-mgmt.json`: every confirm sentence equal to BEFORE, the deny dialog loaded from the module, the bulk notices unchanged) before the expansion task. The behaviour facts were written first but not run red before the tracer edits (a single sequential pass).

**2. [Rule 2 - Missing] Two shared partials.** `_ShopManagementDialogs.cshtml` and `_ShopManagementStockBadge.cshtml` are new files not in the plan's file list; they remove duplicated dialog markup between the twin views and duplicated stock-badge markup between two tables. Both are in the guard lists (no inline code, antiforgery token scan, planning-reference scan).

**3. [Rule 1 - Bug] Empty-section text contrast.** The sentence under an empty section's heading measured 4.1:1 on the aside card; `.mgmt-section .empty-state-text` now uses the card colour.

**4. Drawing versus today's actions on the phone.** The approved drawing showed Edit & Resubmit on a denied phone slip; the phone keeps today's actions (Edit already leads to the same page). The drawing's Denial Reason block on the phone slip is included (new on the phone, as the plan's "desktop's words" intends).

**5. Scratch-tool changes (outside the repository).** `manifest.mjs` rows `mgmt-deny-dialog` (new trigger and name hooks) and `mgmt-index-empty` (both layouts now show No Items Yet and Create First Item) were updated and `manifest.json` regenerated; `mgmt-probe.mjs` was written.

## Known Stubs

None. Bulk Actions shows its "future update" notice by design (real bulk actions are a deferred idea, not part of this plan).

## Threat Flags

None. T-99-34 mitigated (every form keeps its antiforgery token; `ShopPostForms_EachCarryAnAntiforgeryToken` scans both views and both partials). T-99-35 mitigated (the item name travels in the Razor-encoded `data-deny-name` and is written with `textContent`; guard forbids markup-from-strings). T-99-36 mitigated (the Deny URL is server-rendered per item; controller unchanged). T-99-37 mitigated (view conditions for Publish, Deny and Delete kept exactly; render tests run an administrator and a Dungeon Master with another Dungeon Master's item).

## Self-Check: PASSED

Present: `shop-management.js`, `_ShopManagementDialogs.cshtml`, `_ShopManagementStockBadge.cshtml`, `after/99-14/` (mgmt.json, mgmt-before.json, tracer-mgmt.json, report.json, allow.json, sweep captures), `tools/mgmt-probe.mjs`; `shop-management-index.mobile.css` absent. Commits found: 7546fc3b, fa157025. Both suites green; port 8320 stopped, 8321 left running.
