---
phase: 99-shop-and-shop-management-ui-conversion
plan: 17
subsystem: shop-ui
tags: [shop, phone, offcanvas, history, return-sell, pager, empty-states, dialog, guards]
requires: ["99-16"]
provides:
  - converted phone Shop/Index (hero, My Transactions and Filter & Sort drawers, interactive cards, Previous/Next with count, the three desktop empty states, scrollable details dialog)
  - shop-drawers.js (mirrors aria-expanded onto the openers of Bootstrap offcanvas drawers)
  - phone Return and Sell through the shared _ShopPurchaseHistory partial, with the Sell confirmation
  - shop.mobile.css retired
affects: [99-18, 99-19, 99-21]
tech-stack:
  added: []
  patterns: [shared history card inside an offcanvas-start drawer named by its own h2, opener aria-expanded mirrored by a module, stretched name link as the whole-card tap target, same drawer id on both layouts so tools and sweeps need no phone branch]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/shop-drawers.js
  modified:
    - QuestBoard.Service/Views/Shop/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/shop-pages.mobile.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/shop.mobile.css
decisions:
  - "Phone history follows the approved v2 redraw: a My Transactions button stacked above Filter & Sort opens an offcanvas-start drawer holding the shared history card, not a section under the list."
  - "The drawer is named by the shared card's own h2 (history-title); the drawer header holds only the Close button, laid over the card's title row."
  - "The history drawer has the same id (historyPane) as the desktop pane, so the click-through and sweep tools work unchanged on the phone."
  - "The filter drawer form carries the selected category as a hidden type field, as the desktop card does (the old phone form dropped the category on Apply)."
metrics:
  duration: "about 100 minutes of wall clock (sequential single pass)"
  completed: 2026-11-16
status: complete
actuals:
  tokens: 36000
  tasks: 3
  commits: 4
---

# Phase 99 Plan 17: Phone shop Summary

The phone shop now has the same structure as desktop: a My Transactions button that opens a drawer of the shared history slips with visible Return and Sell buttons, one Filter & Sort drawer, foundation cards in two columns, Previous and Next with the count, the desktop's three empty states, and a details dialog that scrolls inside itself; its old sheet is gone.

## What changed

- **History (phone).** `button#historyToggle[data-history-toggle]` (count pill = listed rows, max 15, omitted when empty) sits in `.shop-tools` above Filter & Sort, collapsed on every load (`aria-expanded="false"`, `aria-controls="historyPane"`, `data-bs-toggle="offcanvas"`). `div.offcanvas.offcanvas-start.history-offcanvas#historyPane[aria-labelledby=history-title]` holds the shared `_ShopPurchaseHistory` card in its body; the drawer header carries only a named Close, laid over the card's title row, and the card is flat inside the drawer (`--bs-offcanvas-width: min(22rem, 88vw)`). Return and Sell are the partial's own forms (tokens, `asp-controller`/`asp-action`, the Sell sentence in `data-confirm-message`); `confirm-submit.js` loads on the page. A visitor who is not signed in gets neither the button nor the drawer. `shop-drawers.js` (new) mirrors the drawer state onto every opener of an `.offcanvas[id]` through Bootstrap's `show.bs.offcanvas` and `hidden.bs.offcanvas`, because Bootstrap does not keep `aria-expanded` on an offcanvas opener.
- **Hero, trigger, filter drawer.** `h1.shop-hero__title` at the 20px phone step in gold. `Filter & Sort` is a full-width `btn-secondary` with an Active badge (filter icon and word) when anything is filtered. `#shopFilterOffcanvas.offcanvas-bottom.shop-filter-offcanvas` has an h2 title (`#shopFilterTitle`) and a named Close, on the opaque overlay, at most 70vh tall with the body scrolling past that. It holds the category links (filled `btn-secondary`, the current one `btn-primary` with `aria-current="page"`, Quest Items with `fa-scroll`), a fieldset of rarity rows (`.filter-check-row`, 44px, the chip is the label), Sort, Search, and a footer row with Apply Filters (`btn-primary`) and, when something is active, Clear Filters (`btn-secondary`, keeps the category). The tabs stay in the drawer (Gate 1 item 6).
- **Cards.** `.inventory-grid.shop-grid-mobile` is `repeat(auto-fill, minmax(min(156px, 100%), 1fr))`: two columns at 360 to 412, one at 320. Each card is `modern-card modern-card--interactive shop-card item-card` with `data-item-card`, an h3 `row-nav-link` stretched over the whole card, the chip, the price and a full-width `btn-primary` Buy (Login to Buy for a visitor). No description, no stock label, no merchant on the phone.
- **Pager, count, empty states.** Previous and Next are two 44px `btn-secondary` links with the desktop's aria-labels, `aria-disabled="true"`/`tabindex="-1"` and muted colours at the ends, then `Showing x–y of n items`; the numbered window and the whole `PageWindow` helper are gone. The three empty states are the desktop's, word for word, in a `modern-card` under a visually hidden `h2 Items`: "No items match your search" with Clear search, "No items match your filters" with Clear filters, "The shop is currently empty". This replaces the phone's old generic "No items found".
- **Dialog.** `#itemDetailsModal` is `modal-dialog-scrollable` without `modal-lg`, on the foundation surface (no `bg-dark`, `text-light`, `border-secondary`), named by an h2 title through `aria-labelledby`, with a named Close; `shop-item-modal.js` fills it. The inline fetch and `innerHTML` script and both inline `onclick` handlers are gone. The view carries no style attribute, style block, inline script or on-event attribute.
- **Retired.** `shop.mobile.css` is deleted (its glass card, 2-line clamp, `!important` colour and text-shadow rules, the `.shop-header-card`, `.shop-empty-state` and `.shop-pagination-mobile` blocks). The view links `shop-pages.css`, `shop-pages.mobile.css` and `markdown-content.css`.

## Click-throughs and probes (port 8320, p99player, board "P99 Audit Shop", Pixel 7 User-Agent with touch)

| Check | Result |
|---|---|
| Return on a fresh purchase from the phone drawer (`history-click.mjs 8320 return ... --mobile`, `after/99-17/return-phone.json`) | POST 302; row after reads "Ember-Forged Traveler's Pack (Fully Returned/Sold) 1 25.00 gp 16 Nov"; no confirm shown; `mobileLayout` true |
| Sell click-through | pending for the closing sweep: `fixtures.mjs --sell-status` is not eligible until 2026-11-17T11:17:54Z; nothing was clicked |
| Drawers with the real keyboard (`shop-phone-probe.mjs`, `after/99-17/phone-probe.json`, 390 and 360) | both drawers closed by default; Enter on the opener opens, focus moves inside, role dialog and aria-modal true, Tab and Shift+Tab stay inside for 30 presses each way; Escape, the Close button and a backdrop tap each return focus to the opener and set `aria-expanded="false"`; open sets it to `"true"`. History drawer body scrolls (2422px content in an 844px drawer); filter drawer is 591px in an 844px window (70% cap) and its body scrolls |
| Dialog (390, long-name fixture and 1600x1067 wide-image fixture) | dialog 828px in an 844px window, body scrolls (1465 and 1575px of content in 765px), page behind locked, 1600x1067 picture shown whole at 290x193 (ratio 1.4995 against 1.4995), Escape returns focus to the name link; a tap on an empty part of a card opens the dialog |
| Layout | columns 2, 2, 1 at 390, 360, 320; no horizontal overflow at any of them; hero h1 20px, gold |
| Sweep (`after/99-17/report.json`, 14 manifest rows, 48 captures, phone widths 390 and 412, desktop 320, 768, 1024, 1440) | all 200; no horizontal overflow on any row (phone at 390/412 and desktop at 320); no phone target under 44px (the smallest measured is 44x44, a stretched card link counted as the whole card); no contrast failure on the phone rows; no missing focus ring; no console or page error; no failed assert or action; `readability-compare.py` against BEFORE with `after/99-17/allow.json`: ok |

The 18 day-0 aging purchases were never clicked. One fresh purchase was made (`--force-one`) and returned; the sweep's `--fresh-purchases` made none ("0 (20 purchases younger than 20 hours)").

## Tests

- Unit tests 1424 passed. Integration tests 2544 passed, 16 skipped (SQL Server migrator tests), 2560 total (2527 passed before; +17).
- New facts: 4 phone history facts and 11 phone browse facts in `ShopRenderTests` (toggle and drawer, Return and Sell forms with token and the Sell sentence, empty history, visitor, hero/trigger/cards, Active badge and category kept, filter drawer shape, pager and count with no numbered window, one item, three empty states, Clear search, encoded search text, dialog shell and modules, no inline code); `ShopDrawersModule_...` in `KeyboardScriptGuardTests`; `ConvertedViews` gained `Shop/Index.Mobile.cshtml`, `RetiredFiles` gained `shop.mobile.css`, `ShopModules` and `PhaseSourceFiles` gained `shop-drawers.js`, the deleted sheet left the lists that read it.
- Renamed or reworded (also in `tests-renamed.md`): `MobileShopIndex_MobileUserAgent_OmitsPurchaseHistoryPanel` became `MobileShopIndex_MobileUserAgent_RendersPurchaseHistoryWithReturnAndSell` (seeds a purchase; asserts the shared card, a Return form with a token and no `purchase-history-panel`); `MobileShopIndex_MobileUserAgent_RendersItemGridAndFilterButton` asserts the new sheet names and no `shop.mobile.css`; the rarity-chip card fact's phone marker is `data-item-card`. No test deleted.

## Deviations from Plan

**1. [Gate 1 redraw] The phone history is a drawer, not a section under the list.** The plan (and its verify wording "after the item list") describes an in-flow My Transactions section under the grid and pager. Per the operator-approved v2 redraw the same collapsed toggle opens an `offcanvas-start` drawer, and My Transactions is stacked above Filter & Sort. The flipped fact asserts the shared card, the Return form and its token inside the drawer, and the order toggle, filter button, grid. The drawer shares the desktop pane's id (`historyPane`), so `history-click.mjs --mobile` works without a branch.

**2. [Rule 3 - Blocking] Tracer feedback gate.** Auto mode is off and a sequential executor cannot be resumed after a checkpoint, so the tracer was committed and then proven in a real browser (the phone Return above) before the rest of the page was built.

**3. [Rule 1 - Bug] Probe measured the stretched name link as a 22px target.** The probe tool's stretched-link rule knew `.member-card` and the like but not `.shop-card`, and its focus-ring rule did not know the card either, so every phone card link was reported as a small target with no ring. Both rules now include `.shop-card` (the `::after` position is still checked, and a tap on an empty part of a card opening the dialog is proven in `phone-probe.json`). Backup `tools/probes.mjs.pre-99-17`.

**4. [Rule 1 - Bug] The phone filter form dropped the selected category.** The old phone form posted no `type`, so Apply Filters reset the category to All Items. The new form carries a hidden `type` field when a category is chosen, as the desktop card does; the query-string contract is unchanged.

**5. Fixture mishap and cleanup (scratch data, not the repository).** Running `fixtures.mjs --force-one` without `--fresh-purchases` ran the default setup steps; the tracer step's management-list scrape (`.item-rarity`, `strong`) no longer matches the converted list, so it created a second published "Ember-Forged Traveler's Pack" (item 5485, p99dm, board "P99 Audit Shop"). It was archived through the app's own Archive form (a non-admin cannot delete a published item) and no longer shows to players; it stays as an Archived row in p99dm's management list (noted for the closing sweep). No other fixture changed.

**6. Tests for the browse half were written alongside the view.** The Task 1 facts were run red first (5 failed before the view change); the Task 2 facts were written after the view in the same pass, so no separate red run exists for them. `LegacyCollision` and the guard scans pass.

**7. Scratch tooling (outside the repository).** `manifest.mjs` and `fixtures.json` changed so the phone rows assert the desktop's empty-state words and the history rows open the drawer on the phone too; `shop-index-history-phone` now opens the drawer and asserts a Return button; backups `manifest.mjs.pre-99-17`, `manifest.json.pre-99-17`, `fixtures.json.pre-99-17`. New tool `shop-phone-probe.mjs`. `allow.json` carries the phone relayout (numbered page links gone, Previous and Next added, card links, history drawer, dialog named by its title and a named Close) and the desktop main-landmark renames for the rows 99-15 and 99-16 did not sweep.

## Known Stubs

None.

## Threat Flags

None new. T-99-45 mitigated (the phone Return and Sell forms are the shared partial's `asp-controller`/`asp-action` forms: the flipped fact and `ShopIndex_Phone_...ReturnFormWithATokenAndAThirtyHourOneASellThatAsks` assert the token, and the phone Return landed). T-99-46 mitigated (the search text is a Razor-encoded `value` only; `ShopIndex_Phone_ReflectsTheSearchTextAsAnEncodedValueOnly` posts a script string and asserts it is encoded).

## Self-Check: PASSED

Present: `Index.Mobile.cshtml`, `shop-drawers.js`, `shop-pages.mobile.css`; `shop.mobile.css` absent; `after/99-17/` (return-phone.json, phone-probe.json, report.json, allow.json, screenshots). Commits found: a8503ada, 9dc25ed5, 491d0649. Both suites green; controllers unchanged since the plan start; port 8320 stopped, 8321 left running; no background process started by this plan is running.
