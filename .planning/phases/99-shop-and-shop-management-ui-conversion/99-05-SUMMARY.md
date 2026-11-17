---
phase: 99-shop-and-shop-management-ui-conversion
plan: 05
subsystem: gate-1-mockups
tags: [ui-review, gate-1, mockups, shop, look-check, merchant]
requires: ["99-04"]
provides:
  - Gate 1 package A drawn as scratch mockups under ~/qb-ui-review/phase-99/mockups/v1/ (34 frames, each with its BEFORE manifest id and a side-by-side pair PNG)
  - draft shippable shop-pages.css and shop-pages.mobile.css (one @layer pages block each) that the Shop/Index slices port
  - lookcheck.json, a computed-style equality table per mock against the Direction A shop render (23 mocks, 908 rows, every difference equal or one of the intended ones)
  - mock-scan.py (with a self-test), mockup-shoot-99.mjs, lookcheck-99.mjs, build-mocks-99.py, build-pairs-99.py as scratch tools
affects: [Gate 1 package assembly, the Shop/Index slices, the merchant slice, the chip fix]
tech-stack:
  added: []
  patterns: [static mocks built from fixture data and captured from file:// with no server port, a scanner that keeps every mock sheet inside the shop contract, computed-style rows beside the signed-off render]
key-files:
  created: []
  modified: []
decisions:
  - "No merchant placement is recorded as chosen; both are drawn and the pick is the operator's at Gate 1 item 3"
  - "The phone hero title is drawn gold, as the signed-off render draws it, because only eight differences from the render are allowed and the colour is not one of them; the UI-SPEC says parchment in one table and gold in another (decision item below)"
  - "The legendary chip is drawn amber in every mock (the render) and gold only on the comparison page, so Gate 1 item 16 keeps its default"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 19000
  tasks: 3
  commits: 1
---

# Phase 99 Plan 05: Gate 1 Package A Summary

Gate 1 package A is drawn: the purchase history under the grid on both layouts, the single filter card, both merchant placements with the toast, the legendary chip both ways, and the whole Shop/Index page on both layouts (populated and empty), each captured from `file://` with the bundled Chromium and measured against the Direction A shop render. No repository source file was created or changed; the only commit is this docs commit.

Package path: `~/qb-ui-review/phase-99/mockups/v1/` (`site/`, `history/`, `filter-card/`, `look-index/`, `chips/`, `merchant/`, `png/` and `png/pairs/`, `frames.json`, `lookcheck.json`).

## What was built

- **Provenance.** `site/` holds `tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css`, `markdown-content.css`, `mobile.css` and `app.css` copied from the working tree at commit `c556f7109acd3e1dd1374369ba3ac5853e732419` (written to `site/PROVENANCE.txt`), with the vendor files under `site/lib/`. `app.css` has only its relative `../lib/` paths changed to `lib/`. `shop-pages.css` and `shop-pages.mobile.css` are the new drafts, `chip-alt.css` is the one comparison-only sheet.
- **Data.** Names, prices, rarities, stock labels, excerpts and the 15 purchase rows come from the p99player fixture on the frozen baseline (one read-only GET of /Shop pages 1 to 3, saved in `data/shop-data.json`); the 20 merchant quotes are read from `Shop/Index.cshtml` at build time, so they are verbatim. No POST was made and no SQL was written.
- **Whole image rule.** No `aspect-ratio` or `object-fit` in any mock sheet (`grep -cE "aspect-ratio|object-fit" site/shop-pages.css` prints 0).
- **Scanner.** `tools/mock-scan.py mockups/v1` reports 23 files scanned and 0 hits; `--self-test` catches all 18 seeded bad strings (both crop properties, an important flag, hex, rgb(), rgba(), hsl(), `transition: all`, `infinite`, an animation outside the no-preference block, `innerHTML`, `insertAdjacentHTML`, inline style, inline handler, inline script, a style element) and passes a clean sample, plus a two-sheet `@keyframes` count.

## Frames (frame name, BEFORE manifest id)

All 34 frames record `overflowX: false` (`scrollWidth <= innerWidth`) and 0 console, page or request errors. Each has a side-by-side pair (BEFORE left, drawing right) in `png/pairs/`.

**Purchase history (Q-120 desktop, D-10 and Q-56 phone).** `history-desktop-1440` (shop-index), `history-desktop-1024` (shop-index-1024), `history-phone-390` (shop-index; the phone baseline draws no history, the after-only row is shop-index-history-phone), `history-empty-desktop` (shop-history-empty), `history-empty-phone` (shop-index). In `history-desktop-1440.png`: the first slip, Ember-Forged Traveler's Pack, shows the green Return (1) button; Cloak of the Quiet Heron (`1/2`, dated Nov 14) shows the amber Sell (1) button; Dewdrop Pendant shows the words (Fully Returned/Sold) and no button; its ledger twin reads (Sold/Returned) with +115.00 gp in the success text; the long name (Ceremonial Gauntlets of the Thrice-Crowned Warden of Highmere Reach) wraps to three lines and shows Return (3). All buttons are ordinary in-flow buttons, pinned to the foot of the slip, with no opacity or visibility trick; the "Showing latest 15 purchases" line follows the 15 slips. The Sell, fully returned and ledger rows are composed from fixture names and prices because no purchase is 24 hours old yet (the 18 day-0 purchases were not touched); their dates are drawn as Nov 14.

**Filter card (Q-121, Q-61).** `filter-1440-none`, `filter-1440-search`, `filter-1024-none`, `filter-1024-search`, `filter-768-none`, `filter-768-search` (all shop-index), and `filter-phone-offcanvas` (shop-index). The Active badge and Clear Filters appear only in the search state.

**Shop/Index look check pages.** `look-index-desktop`, `look-index-phone` (shop-index), `look-index-empty-desktop`, `look-index-empty-phone` (shop-index-empty), plus `look-index-empty-search-desktop|phone` (shop-index-search-empty) and `look-index-empty-filter-desktop|phone` (shop-index-filter-empty) so all three verbatim empty states are drawn.

**Chip.** `chip-legendary-both` (shop-modal): amber on the left (`--qb-warning` on `--qb-warning-fg`, 8.92:1 on the card and in the dialog), gold on the right (`--qb-accent` on `--qb-accent-ink`, 11.55:1 both places); the ratios are computed from the rendered fills and recorded in `frames.json`.

**Merchant (D-01, D-02).** `merchant-a-1440-top`, `merchant-a-1440-bottom`, `merchant-a-1024-top`, `merchant-a-1024-bottom`, `merchant-a-toast`, `merchant-a-focus`, `merchant-a-reduce`, `merchant-b-1440-top`, `merchant-b-1440-bottom`, `merchant-b-1024-top`, `merchant-b-1024-bottom`, `merchant-b-toast`, `merchant-b-focus` (BEFORE ids shop-index, shop-index-1024 or shop-merchant-toast). The toast and focus frames are reached with the real Tab and Enter keys; the toast quote in the frames, copied verbatim from `Shop/Index.cshtml`, is "Coin up front, no credit! But I guarantee satisfaction!", the header is the amber one with `fa-user-tie` and "Mystical Merchant", the Close button is named, and the quote is also written into the visually hidden status region.

## Dock clearance and bob

`dockFooterOverlap` is `false` on all four placement (a) top and bottom frames. At 1440 the dock and button rest at y 750 to 806 and the footer starts at y 838.6 (32 px clear); at 1024 the same. Bob evidence (sampled from load for 5 s): placement (a) at no-preference shows `merchant-bob`, 1.5s, iteration count 3, a maximum excursion of 4 px, and still at the end; under emulated reduce (`merchant-a-reduce`) the animation name is `none` and the transform never moves. `grep -c "infinite"` on `shop-pages.css` prints 0; the sheet holds exactly one `@keyframes` and the only `animation` declaration sits inside the no-preference block.

## Look check results

`lookcheck.json` holds 23 mocks and 908 rows (hero, hero title, card, page panel, title band, slip, overlay, five chips, commit button, page backdrop, empty-state card, hover glass token). Every row is equal to Direction A or carries one named intended difference, named at most once per mock: `btn-border` (the render draws a 50% white border, production a transparent one), `legendary-glow` (still glow, the render has none), `filled-secondary` (Apply Filters filled, the render outlined), and `phone-navy` (the phone backdrop image; the render draws the oak board). The hover glass value is equal (no difference to name). The structural differences (history as an in-flow section and one filter card; the phone keeping its category links in the offcanvas) are listed per entry. All 23 entries pass, including the four `look-index-*` mocks the plan names.

## Gate 1 decision items found while drawing

1. **Merchant placement (item 3, no default).** Both are drawn; nothing is recorded as chosen.
2. **Legendary chip colour (item 16).** Amber is drawn everywhere; gold is on the comparison page. Default stays amber.
3. **Phone hero title colour.** The UI-SPEC says parchment in "Page and card title colour" and gold in its Typography table. The mocks draw gold (the render), because a parchment title would be a ninth difference from the render. If the operator wants parchment it is a one-line change in `shop-pages.mobile.css` and a ninth named difference.
4. **Phone filter drawer height.** The current drawer reads as Bootstrap's 30vh bottom sheet with a 70vh cap that is never reached; the mock draws the drawer as tall as its controls up to 70vh so every control is visible without scrolling (`filter-phone-offcanvas`, a 1700 px tall viewport). Keeping 30vh is the literal "unchanged".
5. **Long item names on the card.** The spec fixes the chip as a non-shrinking item beside the name, so the 66-character fixture name wraps to seven lines in a narrow column (see `merchant-a-toast.png`). An alternative is to let the chip drop under a long name.
6. **Empty states.** Drawn as an `.empty-state` inside a non-interactive card so the empty page reads like the render (which draws a card); the spec says only `.empty-state`.

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate satisfied by an automated end-to-end check**
- **Found during:** Task 1.
- **Issue:** the run is not in auto mode, so the gate asks for a human-verify checkpoint; this sequential run cannot be resumed after a checkpoint (same as plan 99-04).
- **Fix:** the tracer's `<verify>` was run end to end (scan, self-test, frame, BEFORE link, look-check, provenance) and the frame was read before any expansion task. Tracer verified end-to-end, expanding. The operator can still look at `png/history-desktop-1440.png` and `png/pairs/history-desktop-1440.png`.

**2. [Rule 3 - Blocking] The inherited `mockup-shoot.mjs` and `lookcheck.mjs` are Phase 98 tools**
- **Issue:** they are built around the group picker and the quest pages (selectors, a Quest-page map, a throwaway server). The plan says "adapt".
- **Fix:** new scratch tools `mockup-shoot-99.mjs` and `lookcheck-99.mjs` (from `file://`, no server port), with `look-spec-99.json` holding the role-by-role rows and the intended-difference ids; the Phase 98 tools were left alone.

**3. [Rule 1 - Bug] Full-page captures lost the fixed backdrop below the first screen**
- **Issue:** the board backdrop is a fixed 100vh layer, so a plain full-page capture showed the fallback colour under the fold.
- **Fix:** the capture grows the viewport to the page height first.

**4. [Rule 1 - Bug] Keyboard focus scrolled the page before the toast frame**
- **Fix:** after the real Tab reaches the merchant button the page is scrolled back to the top (focus stays on the button) before Enter is pressed.

**5. The scanner exempts the eight copied foundation sheets**
- `components.css` carries the shared `!important` visually-hidden helper and an unrelated `object-fit: cover` on the 32 px avatar (`.avatar-32`), and `tokens.css` carries the hex values. They are the repository's own files, copied unchanged, so the scanner skips them (listed with `--list`) and scans everything the plan wrote: both shop sheets, `chip-alt.css`, `mock-merchant.js` and all 19 pages.

**6. The Sell, fully returned and ledger rows are composed**
- Real purchases are all younger than 24 hours, so only Return rows exist in the data. Names, quantities and prices are the fixture's; the states and the Nov 14 dates are drawn. Nothing was returned or sold.

**7. Additional frames beyond the plan's list**
- `filter-1440-*`, `filter-1024-search`, `filter-768-none`, the search and filter empty-state pages and `merchant-b-1024-bottom`, so every state in the contract exists at every width it names. `png/pairs/` (BEFORE beside drawing) was added for the Gate 1 package.

## Known Stubs

None in the repository (no repository file was created or changed). The mocks use `href="#"` and `action="#"` on purpose; nothing is wired because they are drawings.

## Threat Flags

None. The mocks hold only fixture item names and amounts. A grep of the package for `localhost`, `127.0.0.1`, `@example`, the repository owner's name and symlinks finds nothing (the BEFORE captures that `png/pairs/` places beside the drawings show the baseline's own footer credit and the fixture account display name; the package assembly plan applies the hygiene check to the final package).

## Self-Check: PASSED

Present: `mockups/v1/frames.json` (34 frames), `lookcheck.json` (23 entries), `site/PROVENANCE.txt`, `site/shop-pages.css`, `site/shop-pages.mobile.css`, `tools/mock-scan.py` (scan 0 hits, self-test ok), `png/pairs/` (34 files). `history-desktop-1440` carries beforeId `shop-index`; the four `look-index-*` look-checks pass; `merchant-a-1440-bottom.dockFooterOverlap` is false; `git status --porcelain -- QuestBoard.Service QuestBoard.IntegrationTests` prints nothing.
