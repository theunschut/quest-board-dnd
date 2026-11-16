---
phase: 99-shop-and-shop-management-ui-conversion
plan: 16
subsystem: shop-ui
tags: [shop, history, return-sell, merchant, toast, live-region, es-module, guards]
requires: ["99-15"]
provides:
  - _ShopPurchaseHistory.cshtml (the one purchase history card both layouts render) with visible Return and Sell buttons
  - UserTransactionViewModel.CanReturn and SellRefund (the single 24-hour clock read and the Sell payout)
  - desktop My Transactions as a collapsed, toggled side pane (shop-history.js)
  - shop-merchant.js (accessible merchant toast from server templates, persistent polite live region)
  - desktop Shop/Index fully converted and in ConvertedViews; old panel, return-sell, merchant, merchant-toast and add-character rules deleted
affects: [99-17, 99-18, 99-19]
tech-stack:
  added: []
  patterns: [disclosure button controlling a hidden pane, server-rendered quote and toast templates cloned by a module with textContent, sticky dock that rests after the content, single capped keyframes inside the no-preference query, view model owning the one clock read]
key-files:
  created:
    - QuestBoard.Service/Views/Shared/_ShopPurchaseHistory.cshtml
    - QuestBoard.Service/wwwroot/js/shop-history.js
    - QuestBoard.Service/wwwroot/js/shop-merchant.js
    - QuestBoard.UnitTests/ViewModels/UserTransactionViewModelTests.cs
  modified:
    - QuestBoard.Service/Views/Shop/Index.cshtml
    - QuestBoard.Service/ViewModels/ShopViewModels/UserTransactionViewModel.cs
    - QuestBoard.Service/ViewModels/ShopViewModels/ShopIndexViewModel.cs
    - QuestBoard.Service/wwwroot/css/shop-pages.css
    - QuestBoard.Service/wwwroot/css/shop.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs
decisions:
  - "History follows the approved v2 redraw (collapsed My Transactions toggle row under the hero opening a sticky side pane), not the plan's in-flow-under-the-grid text."
  - "Merchant placement a (round button in a sticky dock), amber legendary and Sell-row confirmation per the Gate 1 answers."
  - "The shop view model clock scanner allows the two pre-existing availability-window reads in ShopItemViewModel besides the one CanReturn read."
metrics:
  duration: "about 17 minutes of wall clock (sequential single pass)"
  completed: 2026-11-16
status: complete
actuals:
  tokens: 27000
  tasks: 3
  commits: 2
---

# Phase 99 Plan 16: History and merchant Summary

Desktop Shop/Index is finished: My Transactions is a toggle under the hero that opens a sticky side pane of slips whose Return and Sell buttons are ordinary visible buttons, and the merchant is a named, keyboard-operable round button whose quote is announced through a persistent live region and shown in a self-hiding toast.

## What changed

- **History.** `.shop-toolbar` holds `button#historyToggle[data-history-toggle]` (aria-expanded false on every load, aria-controls `historyPane`, count pill = listed rows, max 15, omitted when empty, visually hidden word "purchases"). `section#historyPane[hidden]` sits at the start of `.shop-layout`, beside `.shop-main` (tabs to the count line). When open it is a sticky 17rem pane and the grid drops a column; below 768px it opens above the items. `shop-history.js` flips aria-expanded and `hidden`, remembers nothing. The card is the shared partial `_ShopPurchaseHistory.cshtml`: h2 "My Transactions" with `fa-history`, `ul.history-grid > li.history-slip` (name in full, "(Fully Returned/Sold)" for a spent row, quantity or remaining/n, amount with the coin, `Html.LocalTime(..., "date-compact")`), the "Showing latest 15 purchases" line and the empty state.
- **Return and Sell.** One form per slip while something is left, by `purchase.CanReturn`: Return (`btn-success`, full price, never asks) or Sell (`btn-warning`, half price). Each posts through `asp-controller`/`asp-action` (token written), the visible name stays "Return (n)" or "Sell (n)", and the old title text is the button's description through a visually hidden sibling and `aria-describedby`. The Sell form carries `data-confirm-message` "Are you sure you want to sell {n}x {name} to the shop for {total} gp?" (name Razor-encoded; total `SellRefund` with thousands separators); `confirm-submit.js` loads on the page.
- **Clock.** `UserTransactionViewModel.CanReturn` is the one read (a real instant, `<= 24h`), `SellRefund` is `Price / Quantity * RemainingQuantity * 0.5m` (zero when nothing remains). The inline computation left `Index.cshtml`; no shop view reads a clock (`ShopViews_ReadNoClock`). `ShopIndexViewModel` gained `PurchaseHistoryLimit` (15) and `ListedPurchaseCount`.
- **Merchant (placement a).** `button.shop-merchant[data-merchant-button]` with `aria-label="Talk to the merchant"` in `.shop-merchant-dock`, the last child of `.shop-page`; `template[data-merchant-quotes]` holds the 20 quotes word for word (pinned in `ShopRenderTests`), `template[data-merchant-toast]` the toast (role status, aria-live off, named Close), and `div[data-merchant-live]` (role status, aria-live polite) is on the page before any click. `shop-merchant.js` picks a quote, clones the toast into `.toast-container` (one merchant toast, reused on a second press), writes the quote with `textContent`, shows it through `bootstrap.Toast` (6000 ms, held while hovered or focused by Bootstrap), removes it on `hidden.bs.toast`, and fills the live region (cleared first so a repeat is announced); without a container it still fills the region. No inline handler, no `position: fixed`.
- **Motion.** One `@keyframes merchant-bob` (transform only, 4px), run `1.5s ... 3` inside `prefers-reduced-motion: no-preference` only. Probe: 3 iterations, finished after 5.5s, no running animation under reduce.
- **Rules deleted.** From `shop.css` and `legacy-pages.css`: the whole fixed `.purchase-history-panel` family, `.purchase-item*`, `.purchase-name`, `.purchase-details*`, `.shop-container.with-panel` and its three media blocks, `.return-sell-buttons*`, `.shop-merchant*`, `.merchant-toast*` and the unused `#addCharacterModal` block (about 100 rules in all; legacy-pages.css now holds one rule, shop.css the four filter rules left for the cleanup plan). The merchant's focus ring is restated in `shop-pages.css` because the button's own shadow sits in the pages layer above the base ring.

## Click-through and probe results (port 8320, p99player, board "P99 Audit Shop")

| Check | Result |
|---|---|
| Return on a fresh purchase (Ember-Forged Traveler's Pack, purchased 16:46:43Z) | POST status 302, row after reads "Ember-Forged Traveler's Pack (Fully Returned/Sold) 1 25.00 gp 16 Nov"; no confirm shown |
| Sell click-through | pending for the sweep plan: `--sell-status` eligible false, eligibleAt 2026-11-17T11:17:54Z; `history-click.mjs ... sell` found no Sell button and clicked nothing |
| Merchant probe (`after/99-16/merchant.json`) | pass: Tab reaches it (51 stops), name "Talk to the merchant", 56px, ring 2px solid with the 2px gold inner band, Enter shows one toast whose text is one of the 20 quotes (quoteCount 20), live region matches, second Enter keeps one toast, toast held 7s while hovered and hidden after the pointer leaves, clear of the footer at 1440 and 1024, reduced motion still, bob 3 iterations, 0 page errors |
| Sweep (`shop-index`, `shop-index-1024`, `shop-history-empty`, `shop-merchant-toast` plus new `shop-index-history-open` at 1440/1024/768) | all 200; desktop 0 contrast failures, no overflow, no small targets, no missing ring, no console error; `readability-compare.py` against BEFORE with `after/99-16/allow.json`: ok |

The 18 day-0 aging purchases were never clicked: `history-click.mjs` refuses any purchase not newer than the newest aging purchase.

## Tests

- Unit tests 1424 passed (1416 before; +8: `UserTransactionViewModelTests`). Integration tests 2527 passed, 16 skipped (SQL Server migrator tests), 2543 total (2505 passed before; +22).
- New facts: 13 in `ShopRenderTests` (toggle and collapsed pane, modules, empty history, anonymous visitor, Return, Sell with the encoded `Wizard's "Staff"`, thousands separator and remaining quantity, spent row, slip contents, sixteen purchases, merchant button and dock, the 20 quotes and templates, phone has no merchant); in `ShopConversionGuardTests` the two clock scanners with self-tests, the history partial source fact, the motion guard and its self-test, widened retired-class scanner; `KeyboardScriptGuardTests` two module facts. The existing Return-posts-a-redirect fact keeps passing.

## Deviations from Plan

**1. [Gate 1 redraw] History is the collapsible side pane, not in flow under the grid.** The plan text (and its drawn references) describe an in-flow section under the grid and pager; the operator approved the v2 redraw, so a "My Transactions" toggle row under the hero (count pill, collapsed on every load, aria-expanded and aria-controls) opens a sticky ~17rem pane at the start of the item area, the grid drops a column, and below 768px the pane opens above the items. Facts and the click-through were adapted (the click opens the pane first). A new small module `shop-history.js` (not in the plan's file list) drives the disclosure; the partial renders the card only and the view owns the wrapper.

**2. [Rule 3 - Blocking] Clock scanner allows two existing reads.** The plan says the shop view models hold exactly one `DateTime.UtcNow`, but `ShopItemViewModel.IsAvailable` already reads it twice for the availability window (real instants, not touched by this plan). `ViewModelClockBreaks` allows exactly those two (lines naming the availability bounds) plus the single `CanReturn` read, bans every `DateTime.Now` and `Today`, and has a self-test. `AmbientClockSeamTests` is untouched.

**3. [Rule 3 - Blocking] Tracer feedback gate.** Auto mode is off and a sequential executor cannot be resumed after a checkpoint, so as in earlier slices the tracer was committed and then proven in a real browser (Return on a fresh purchase, above) before the merchant was built.

**4. [Rule 3 - Blocking] Fresh purchase needed a forced flag.** `fixtures.mjs --fresh-purchases` makes nothing while the 18 aging purchases are younger than 20 hours, so a one-purchase `--force-one` flag was added to the scratch tool (backup `fixtures.mjs.pre-99-16`) and used once. Side effect to know: the history lists the newest 15, so aging purchases 01 to 03 are no longer in the visible list. The fresh purchase is spent (returned).

**5. [Rule 1 - Bug] Legendary-chip guard banned any keyframes in the sheet.** `LegendaryChipBreaks` flagged any `@keyframes` or `animation:` anywhere in `shop-pages.css`, which the approved bob needs. It now checks that no `.badge-rarity` rule animates, with its self-test updated; the motion guard owns the merchant's animation.

**6. [Rule 1 - Bug] Inline-handler scanner false positive.** `var onFirstPage` in the pager matched the `\son[a-z]+=` pattern once Index joined `ConvertedViews`; the two locals were renamed `atFirstPage` and `atLastPage`.

**7. Sell-type slip branch is unreachable today.** `GetUserTransactionsWithRemainingAsync` returns Purchase rows only, so the "(Sold/Returned)" and "+N gp" slip branch (kept as the plan asks) never renders; it is pinned by a source fact, not a rendered one. No service change (forbidden).

**8. Tests written alongside the implementation.** The Task 2 facts were written in the same pass as the code, so a separate red run was not observed for them (Task 1's guard facts did fail before the inline script left the view: 2 of 63 red). The scanner self-tests demonstrate that each guard fires.

**9. Scratch tooling (outside the repository).** `manifest.mjs` regenerated: `shop-history-empty` and `shop-history-sell` open the pane first (desktop-only actions), `shop-merchant-toast` uses the new hooks, new afterOnly row `shop-index-history-open`; `allow.json` carries the main-landmark text-sample rename (the history panel no longer opens the page), the 99-15 filter renames, and the toast Close rename. Backups `manifest.mjs.pre-99-16`, `manifest.json.pre-99-16`.

## Known Stubs

None.

## Threat Flags

None new. T-99-41 mitigated (every row form posts through `asp-controller`/`asp-action`; token scanner, the posting fact and the real Return confirm). T-99-42 mitigated (the Sell sentence is built server-side into an encoded attribute and read by `confirm-submit.js` as text; the `Wizard's "Staff"` fact). T-99-43 mitigated (the toast is cloned from a server template, the quote written with `textContent`; markup-from-strings scanner and the keyboard guard cover the module).

## Planner assumption (screen reader)

A persistent polite live region announcing the quote more reliably than the toast itself is still to be confirmed with a real screen reader in the phase UAT.

## Self-Check: PASSED

Present: `_ShopPurchaseHistory.cshtml`, `shop-history.js`, `shop-merchant.js`, `UserTransactionViewModelTests.cs`, `after/99-16/` (return.json, sell.json, merchant.json, report.json, allow.json, screenshots), `tools/history-click.mjs`, `tools/merchant-probe.mjs`. Commits found: d5074318, 620bd7cc. Both suites green; port 8320 stopped, 8321 left running; controllers, domain and the ambient-clock test are unchanged since the plan start.
