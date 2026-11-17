---
phase: 99-shop-and-shop-management-ui-conversion
plan: 15
subsystem: shop-ui
tags: [shop, browse, keyboard-card, details-dialog, filter-card, pager, empty-states, es-module, guards]
requires: ["99-14"]
provides:
  - shop-item-modal.js (details dialog filled from the opener's address, stale answers ignored, parsed nodes only)
  - desktop Shop/Index browse half on the foundation in the navy theme: hero, named category navigation, one full-width Filter & Sort card, keyboard item cards, re-themed pager, three empty states in a card
  - the .shop-card block reused for the real grid cards (stretched name link, ring drawn on the card, Purchase above the link)
  - the old card, inventory, filter row, tab, pager and empty-shop rules (and the card sweep) deleted from shop.css and legacy-pages.css
affects: [99-16, 99-17, 99-18]
tech-stack:
  added: []
  patterns: [card whose name is a stretched real link with the focus ring drawn on the card through :has(), dialog filled by a module with a request counter, fragment parsed into an inert document and only its nodes moved, status text mixed toward the card colour to hold contrast on a translucent card]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/shop-item-modal.js
  modified:
    - QuestBoard.Service/Views/Shop/Index.cshtml
    - QuestBoard.Service/wwwroot/css/shop-pages.css
    - QuestBoard.Service/wwwroot/css/shop.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/ShopControllerIntegrationTests.cs
decisions:
  - "The filter card is the approved full-width redraw (rarity choices in equal columns, then one row with search, Sort and the actions at the row end), not the first drawing the plan text assumed."
  - "Dialog load error: Gate 1 said no, so the body stays empty on a failed or redirected request and the page holds no error template; a recorded-answer constant in ShopRenderTests pins it."
  - "The grid card stock colours are the status text colours mixed 50% with the card text colour, because the plain colours measured 4.05 to 4.12:1 on the translucent card over the brightest board reference."
  - "The four unscoped filter rules that the plan reserves for the closing slice stay in shop.css; the new card uses shop-filter-label so nothing collides."
metrics:
  duration: "about 3h (sequential single pass)"
  completed: 2026-11-16
status: complete
actuals:
  tokens: 17700
  tasks: 3
  commits: 4
---

# Phase 99 Plan 15: Shop browse Summary

The desktop shop's main page now runs on the foundation in the navy theme: every item card is operable by keyboard (its name is a real link stretched over the card, with the two-tone ring drawn on the card and Purchase above the link), the details dialog is filled by a small module that never shows a stale item, and the filter is one full-width card with a rarity fieldset.

## What changed

- **Cards.** `modern-card modern-card--interactive shop-card item-card` with `data-item-card` and `data-item-url` (the card, the name link and Purchase each carry the address, so the module can read it from whichever opened the dialog). The h3 holds `a.row-nav-link` (`href /Shop/Details/{id}`, `data-bs-toggle="modal"`, `data-bs-target="#itemDetailsModal"`), so without script the link opens the full details page. Chip beside the name, 120-character excerpt, price with the coin, stock label with its existing words and a state modifier, and a filled `btn-primary` Purchase (disabled when unavailable). No inline handler remains on a card; the card's stopPropagation script is gone.
- **Dialog.** `#itemDetailsModal` is the foundation shell: `modal-dialog modal-lg modal-dialog-scrollable`, `aria-labelledby`, `h2#itemDetailsTitle` "Item Details" above the fragment's h3 item name, a named Close, no `bg-dark`, `text-light`, `border-secondary` or `btn-close-white`. `shop-item-modal.js` empties the body on `show.bs.modal`, sets `aria-busy`, bumps a request counter, fetches the trigger's `data-item-url`, drops any answer that is not for the latest request (and any that arrives after the dialog closed), parses the fragment with `DOMParser`, removes script elements and moves only the nodes in with `replaceChildren(...)`. A failed, non-OK or redirected answer leaves the body empty (Gate 1 item 14: no).
- **Hero, tabs, filter card.** `h1` in `.shop-hero`; `nav.category-tabs` (aria-label "Item categories") with four 44px links, icon and word, `aria-current="page"` on the current one, Quest Items on `fa-scroll`. One `form#shop-filter-form` inside `.modern-card--aside.shop-filter-card` with an h2 "Filter & Sort" and an "Active" badge whenever a rarity, sort or search is active; fieldset "Rarity" with five checkboxes each labelled by its chip; the search input with a visually hidden label "Search items" and its existing placeholder; Sort; Apply Filters, Search and Clear Filters as filled `btn-secondary`. Clear Filters shows whenever a rarity, sort or search is active and keeps the category. No `form=` attribute and no `style` attribute on the page.
- **Grid, pager, empty states.** `#inventory-grid` with `repeat(auto-fill, minmax(min(280px, 100%), 1fr))` and no blur on the cards; pager keeps `aria-label="Shop page navigation"` and `page-item active`, re-themed through `--bs-pagination-*` tokens, disabled ends carry `aria-disabled="true"` and `tabindex="-1"`, with "Showing x-y of n items"; the three empty states are `.empty-state` inside a `modern-card`, h3 under a visually hidden h2 "Items", verbatim copy and their Clear actions as `btn-secondary`.
- **Rules deleted.** From both `shop.css` and `legacy-pages.css`: `.shop-container` (base), `.shop-header`, `.shop-categories`, `.category-btn`, `.shop-inventory`, `.inventory-grid`, `.item-card` (including the `::before` sweep on `left` and its hover), `.item-header`, `.item-name`, `.item-description`, `.item-footer`, `.item-price`, `.item-stock`, `.purchase-btn`, `.empty-shop`; from `shop.css` also `.shop-filter-row`, `.filter-rarity-group`, `.filter-sort-group`, `.filter-sort-select` and the whole pagination block (63 rules deleted in all: 37 from `shop.css`, 26 from `legacy-pages.css`). The magic shimmer and shine keyframes were already absent from both sheets. `legacy-collision.py` on `Index.cshtml` now lists only history panel (`purchase-*`, `panel-*`, `return-sell-buttons`, `text-muted`, `text-warning`, `btn-success`/`btn-warning` inside the return buttons) and merchant (`shop-merchant`, `merchant-toast`, `toast-header`, `toast-body`) classes, which stay for the next slice.

## Keyboard and dialog results (`after/99-15/keyboard.json`, desktop, p99player, every POST aborted)

| Check | Result |
|---|---|
| Tab from the control before the grid reaches the first card's name link | yes |
| Ring on the card when focused (outline 2px solid dark, inner band `rgb(255, 193, 7)` 2px) and none unfocused | yes (`cardRing` true) |
| Enter opens the dialog and its body fills with that item (h3 "Amberglass Spyglass", title "Item Details") | yes (`enterOpens` true) |
| Focus is inside the dialog | yes |
| Escape closes it and focus is back on the name link | yes (`escReturns` true) |
| Item A slow, closed, item B opened: body shows B after A's answer arrives | yes (`latestWins` true) |
| Details request blocked | body empty, `aria-busy` cleared |
| Long-name item: dialog 844px in a 900px window, body scrolls inside, page does not | yes |
| Wide-image item (1600x1067): shown whole at 716x477, ratio kept | yes |
| Page errors | 0 |

## Sweep and look check

- Sweep on 8320 (`shop-index`, `shop-index-1024`, `shop-index-p2`, `shop-index-p3`, `shop-index-category-empty`, `shop-index-search-empty`, `shop-index-filter-empty`, `shop-index-empty`, `shop-modal`, `shop-modal-wizard`; both User-Agents, 37 captures, all 200): no overflow at 320 desktop, no console or page error, no small targets, no missing ring, **0 desktop contrast failures** (after the stock fix below). `readability-compare.py` against BEFORE with `after/99-15/allow.json` reports `ok`. The allow entries are accessible-name renames: the search field is now named by its label "Search items" and carries an id, Sort loses its colon, the dialog is named by its h2 title and its Close is named; plus the phone dialog's quantity field renamed in an earlier slice (`Quantity` to `quantity`), which the two phone modal rows meet again. Phone rows are otherwise unchanged.
- Look check: the live desktop page against the approved frames (`lookcheck-99.mjs --live`, `after/99-15/lookcheck.json`): every row of the filter frames and the three empty-state frames is equal or an approved intended difference (`legendary-glow`, `btn-border`, `filled-secondary`); `look-index-desktop` reports only "required roles absent: slip, page panel, title band", which is the purchase history built in the next slice.
- Scope: `git diff` of `QuestBoard.Service/Controllers` against the plan start is empty.

## Gate 1 answers applied

- Item 2 (full-width filter card, v2 redraw): built; supersedes the first drawing the plan text refers to.
- Item 14 (no load-error line): no template, body stays empty on failure.
- Item 16 (amber legendary) and item 19 (still glow, filled `btn-secondary`): the existing chip rule and filled secondary buttons.
- Extra decisions: A3 (chip beside the name), A4 (empty states in a card), A14 (foundation ring, drawn on the card), A15 (44px phone targets are the phone slice's; desktop tabs are 44px high).

## Tests

- Renamed to the same intent (recorded in `~/qb-ui-review/phase-99/tests-renamed.md`): `ShopControllerIntegrationTests.Index_WithoutPageParam_Returns12Items_WhenShopHas15` counts `data-item-card`; the desktop marker in `ShopRenderTests.ShopIndex_OneItemOfEachRarity_...` is `data-item-card`.
- New facts: 16 methods (20 cases) in `ShopRenderTests` (theme, keyboard card and Purchase, dialog shell, recorded load-error answer, module load and no inline dialog code, hero and tabs, one filter form with no ties, active badge and Clear Filters with the category, selected state written back, stock words and modifiers, 120-character cut, pager facts, one card, three empty states, Clear search keeps other filters, no old markup), `KeyboardScriptGuardTests.ShopItemModalModule_ClearsTheBodyAndIgnoresStaleResponses`, `ShopConversionGuardTests.OldSheets_HoldNoRuleForTheConvertedBrowseClassesAndNoCardSweepOrShimmer` and its scanner fact; `shop-item-modal.js` joined `ShopModules` and `PhaseSourceFiles`.
- Both red runs were observed before the implementation (7 of 9 failing after Task 1's facts, 13 of 48 after Task 2's).
- Unit tests: 1416 passed. Integration tests: 2505 passed, 16 skipped (SQL Server migrator tests), 2521 total (2482 before; +23).

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate.** Auto mode is off and a sequential executor cannot be resumed after a checkpoint, so as in earlier slices the tracer was committed and then proven in a real browser (`after/99-15/tracer-keyboard.json`: ring, Enter, focus, Escape, latest-wins, blocked fetch all as above) before the expansion task.

**2. [Gate 1 redraw] Filter card follows the full-width redraw.** The plan text and its drawn references describe the first filter-card drawing; the operator approved the redraw, so the card is built from that. The plan's behavioural facts (one form, fieldset "Rarity", hidden label "Search items", Active badge, Clear Filters keeping the category, filled secondary buttons) hold unchanged. Phase notes: the history toggle row (built next) goes between the hero and the tabs; this slice leaves that slot empty and builds no history.

**3. [Rule 1 - Bug] Stock label contrast.** The draft colours (`--qb-info-text`, `--qb-warning-text`, `--qb-danger-text`) measured 4.05, 4.10 and 4.12:1 on the translucent grid card over the brightest board reference and the readability compare flagged them as new failures. Each is now `color-mix(in srgb, <status text> 50%, var(--qb-fg))`, which measures at least 4.5:1; the icon and words carry the state. Commit b264a335.

**4. [Rule 3 - Blocking] Old-class names kept for the next slice.** The wrapper `div.shop-container.with-panel` stays so the old fixed history panel keeps its offset (its base rule is deleted); the filter rules `.filter-label`, `.filter-check-label`, `.filter-apply-btn`, `.filter-clear-btn` stay in `shop.css` as the plan assigns them to the closing slice, and the new card uses `shop-filter-label`. Planning's claim that Contacts/Index renders those four looks like a substring match: that view renders `contact-filter-label` and its siblings, and no view outside Shop/Index renders the exact tokens (recorded in the carry-forward file).

**5. Module hardening beyond the plan.** The fragment's script elements are removed before its nodes are moved (a script parsed by `DOMParser` could otherwise run when attached), and a non-OK or redirected answer counts as a failure instead of being inserted. A closed dialog also drops late answers.

**6. Small copy and behaviour notes.** The hero's quoted welcome line keeps its straight quotes (the drawing shows curly ones). Clear Filters now also shows when only a search is active (the plan's rule). The filter-empty state's "Clear filters" still goes to the bare shop (it dropped the category before too).

**7. No dialog rules added to shop-pages.css.** The approved drafts hold no dialog rules beyond the foundation modal; the shell works on the foundation surface as is, and the scrolling body comes from `modal-dialog-scrollable`.

**8. Look-check tool.** The plan names `lookcheck.mjs`, which has no selector map for the shop drawings; the frame-based `lookcheck-99.mjs --live` was used and its result written to `after/99-15/lookcheck.json` with the required `pass` key.

**9. Scratch-tool changes (outside the repository).** `index-keyboard.mjs` written; `manifest.mjs`, `shop-measure.mjs`, `shop-behaviour.mjs` and `whole-picture.mjs` now open a card with `[data-bs-toggle="modal"][data-item-url*=...]` (the card div also carries the address now and would otherwise be the first match); `manifest.json` regenerated. While finding the right sign-in state, one read-only probe ran as p99player on its default board (not the audit board), loaded pages and opened two dialogs with every POST aborted, and made no change; the stray state file it created was removed and the probe now names the audit board.

## Known Stubs

None. The merchant and the purchase history are untouched by design (next slice).

## Threat Flags

None. T-99-38 mitigated (the address is the server-rendered `data-item-url`, the fragment is parsed into an inert document, scripts are dropped, only nodes are moved; the guard forbids markup-from-strings). T-99-39 mitigated (the search value is written by Razor into the input's value, encoded; no script writes it; a fact checks the value comes back). T-99-40 mitigated (the request counter drops stale answers; failure leaves the empty body and clears `aria-busy`).

## Self-Check: PASSED

Present: `shop-item-modal.js`, `Index.cshtml` (converted), `after/99-15/` (keyboard.json, tracer-keyboard.json, report.json, allow.json, lookcheck.json, lookcheck-live.json, PLAN_START, dialog screenshots, sweep captures), `tools/index-keyboard.mjs`. Commits found: 240adc7e, 04d36d64, b264a335. Both suites green; port 8320 stopped, 8321 left running.
