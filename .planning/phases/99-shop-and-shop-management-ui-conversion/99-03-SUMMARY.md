---
phase: 99-shop-and-shop-management-ui-conversion
plan: 03
subsystem: review-baseline
tags: [ui-review, baseline, before-capture, playwright, shop]
requires: ["99-02"]
provides:
  - frozen baseline app (git archive of the starting commit) served on port 8321, commit in baseline-src/BASELINE_SHA
  - route manifest of 37 rows (34 with BEFORE, 3 afterOnly) with stable ids for every later plan, sweep and gate
  - BEFORE capture of every non-afterOnly row under the desktop and Pixel 7 User-Agents (129 records, all asserts true)
  - baseline behaviour record of every functional break the phase fixes (behaviour.json, measure.json)
  - BEFORE value per owned audit finding (summary.md) and the starting test inventory (tests-before.txt)
affects: [99-04, every later Phase 99 plan, both gates, the closing finding trace]
tech-stack:
  added: []
  patterns: [frozen git archive served beside the working tree, manifest rows built from fixture keys, POST interception at the network layer for every probe that submits]
key-files:
  created: []
  modified: []
decisions:
  - "A row that submits a form stops the POST in the browser with a 204 answer instead of aborting it, because an aborted form navigation leaves Chromium's own error page, which cannot be compared; the behaviour probes still abort (they only record)"
  - "BEFORE came from the current branch at the starting commit, so values differ from the Phase 94 audit wherever the foundation and the v6.0.2 surface already fixed a finding; each owned finding is quoted as measured here"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 12000
  tasks: 3
  commits: 1
---

# Phase 99 Plan 03: Baseline and BEFORE Summary

A frozen copy of the starting commit runs on port 8321, a 37-row manifest addresses every in-scope shop state by a stable id, and BEFORE was captured from that baseline for every row under both User-Agents, together with the baseline behaviour of each functional break. No repository file was created or changed; the only commit is this docs commit.

BASELINE_SHA: `59df0cf84441765483463482c624e9d33a1e0b77` (descended from `887e5dcd` and `c781090c`; the three source trees were clean when it was archived). It is served from `~/qb-ui-review/phase-99/baseline-src` on 8321 and stays running for the later slices. A second `run-app.mjs` against the repository tree on 8321 refuses (exit 1: the port serves a different tree). Port 8320 (the working tree, started by the toolset) was stopped as the plan says.

## Manifest

`manifest.json` holds 37 rows: 34 captured now and 3 afterOnly (`shop-history-sell` needing a Sell-eligible purchase, `mgmt-create-preview`, `shop-index-history-phone`). Row ids: shop-index, shop-index-1024 (desktop 1024 only), shop-index-p2, shop-index-p3, shop-index-category-empty, shop-index-search-empty, shop-index-filter-empty, shop-index-empty, shop-history-empty, shop-modal, shop-modal-wizard, shop-merchant-toast (desktop only), details-published, details-draft, details-denied, details-archived, details-soldout, details-unlimited, details-low, details-dndb, details-window, details-wideimage, details-wizard, details-longname, details-legendary, mgmt-index, mgmt-index-empty, mgmt-deny-dialog, mgmt-bulk-dialog (desktop only), mgmt-create, mgmt-create-errors, mgmt-edit-draft, mgmt-edit-published, mgmt-edit-denied. Paths, roles, boards and asserts come from the fixture keys of 99-02; `needs` is `fresh` on every row that shows the history and `sell` on `shop-history-sell`. The dialog, merchant and deny rows are viewport frames with the dialog open and its body filled (the asserts check the item name inside it); `fixtures.mjs --fresh-purchases` ran before each sweep and made 0 purchases because all 18 day-0 purchases are still younger than 20 hours, so the Return state exists in BEFORE without disturbing them.

## BEFORE capture

129 records (67 desktop, 62 phone) over 34 rows at desktop 1440 and 320, phone 412 and 390 (plus 1024 for `shop-index-1024`). Every record answered 200, evaluated its asserts with none failing, ran every action with none failing, and had 0 console errors, page errors and failed requests. Dialog frames confirmed by reading `before/desktop/shop-modal.png` (Legendary item, body filled, Purchase Item and the Sell to Shop heading visible) and `before/mobile/mgmt-deny-dialog.png` (Deny Item dialog for the Stormglass Orb). Headline numbers (full table in `before/summary.md`):

- Horizontal overflow at 320 and 390 occurs only on `details-wideimage` (the 1600 pixel picture, also at 1440 and 412); every other row fits.
- Lowest contrast ratios: desktop shop 1.63:1 (Legendary chip), Purchase button 2.13:1, pager Previous 2.35:1; phone shop 2.99:1 (subtitle), chips 3.14 to 3.55:1, item name and price 4.34:1; management desktop View Shop 1.31:1 and Denied-on label 1.53:1, phone View Shop 1.7:1; desktop Details 3.27:1; Create and Edit desktop 3.01:1, phone 2.99:1.
- Phone small targets: 1 on Create (both rows), 0 elsewhere.
- The empty Create form submitted through the sweep (POST stopped in the browser) shows no validation message on either layout: the page has no working client check on the item form, so `mgmt-create-errors` currently equals the empty form.

## Behaviour on the baseline

All probes abort their POSTs (3 aborted: the two unconfirmed Sell posts and the item post); the sweep stopped 4 more empty-Create POSTs in the browser. No request reached the server, so nothing was saved.

- Return buttons: 15 of 15 history rows hidden (visibility hidden, opacity 1), not focusable, forms without a verification token. No Sell button exists yet (all purchases are younger than 24 hours).
- Sell to Shop at quantity 2: in the details dialog no confirmation appears (the inline functions never run: `confirmSell` and `updateSellPrice` are not defined) and the total stays at its initial value, so the Sell posts unconfirmed; on the full Details page the confirmation appears ("...sell 2x Brindlewood Shortbow to the shop for 76 gp?", while the page's own total says 75 gp, a rounding difference).
- New finding: for an item whose name holds an apostrophe (Ember-Forged Traveler's Pack) the name is written into the inline handler, which then fails to parse (SyntaxError), so even the full page posts the Sell with no confirmation.
- Preview on Create: no dialog, no element, no request; the click throws `ReferenceError: previewItem is not defined`.
- Edit page View Details: href `/ShopManagement/Details/5461`, GET answers 404.
- Merchant: a `div` with no role and no tabindex, not reached by Tab; the toast has role alert and is built from a template string.
- Create form client check: `document.querySelector('form')` is the navbar Logout form. With the item form empty, clicking Logout is intercepted (alert "Please fill in all required fields.", no logout request); with the item form filled and a 19 character description, the item form raises no alert and the item POST is sent.
- Animations one second after load: `legendary-glow` on every shop page that lists the Legendary item; `mgmt-index`, `mgmt-deny-dialog` and `mgmt-bulk-dialog` run 40 animations with six infinite names (magic-shimmer, magic-shine, quest-pulse, quest-sweep, quest-sparkle, legendary-glow); phone rows run none.
- Purchase history panel: left edge -250 at 320, 768 and 991 with no toggle, on screen from 1000. Phone shop: no history, no Return or Sell form; phone details dialog: 0 Sell forms.

## Owned findings, BEFORE

`before/summary.md` has one line for each of A-shop-001 to A-shop-029 (A-shop-010 noted as fixed separately and proven by 99-02). Where the foundation or the v6.0.2 card surface already fixed a Phase 94 reading, the line says so with the value measured here: A-shop-001 (empty states no longer fail), 003, 004 (dialog text), 005 (dialog title), 007 (teasers), 008, 009, 014 (focus rings present), 019, 021 and the status badges of 022 do not reproduce; A-shop-002 and 020 (phone chip at 4.34:1), 006, 011, 012, 013, 015, 016, 017, 018, 023 (counts and the phone empty state), 024, 025 (phone check labels at 4.34:1), 026, 027, 028 and 029 still reproduce at the values quoted.

## Test inventory

`tests-before.txt` (names plus a `count:` line per project, from `dotnet test --list-tests`): QuestBoard.UnitTests count 1405, QuestBoard.IntegrationTests count 2321. The closing plans compare against this to show no guard or test was deleted.

## Deviations from Plan

**1. [Rule 1 - Bug] The empty Create submit cannot use a plain network abort**
- **Found during:** Task 2, first sweep of `mgmt-create-errors`.
- **Issue:** aborting the form POST leaves the page on Chromium's `chrome-error://chromewebdata/` page, so the capture and its assert were useless.
- **Fix:** a new sweep action `abortPosts` answers every POST with 204 No Content in the browser (the request never reaches the server and the page stays as it is), and `submitEmptyStopped` clicks the submit from the page because a 204 commits no navigation for Playwright to wait for. The behaviour probes keep a real abort.
- **Files modified:** scratch `tools/sweep.mjs`, `tools/manifest.mjs` (not repository files).

**2. [Rule 1 - Bug] The phone dialog body has no `.item-details-content`**
- **Found during:** Task 2, first sweep of the dialog rows.
- **Issue:** the phone Details view renders `.shop-details-card-mobile`, so the wait and assert timed out on the phone.
- **Fix:** the dialog actions and asserts accept either body.
- **Files modified:** scratch `tools/manifest.mjs`.

**3. [Rule 1 - Bug] Two probe selectors were wrong on the first run**
- **Found during:** Task 3. The Preview probe clicked the editor toolbar's own preview button, and the Sell probe's first item had an apostrophe in its name.
- **Fix:** the probe now clicks `button[onclick="previewItem()"]` and uses Brindlewood Shortbow for the clean Sell case, recording the apostrophe item separately (it is the new finding above).
- **Files modified:** scratch `tools/shop-behaviour.mjs`.

Otherwise none - plan executed as written. `shop-measure.mjs` (extra geometry and focus measurements for A-shop-014, 015, 019, 028, 029) and `before-summary-99.py` (builds `before/summary.md`) were added as scratch tools.

## Known Stubs

None. No repository file was created or changed.

## Threat Flags

None.

## Self-Check: PASSED

Present: `baseline-src/BASELINE_SHA`, `manifest.json` (37 rows), `before/report.json` (129 records), `before/behaviour.json` (all eight sections plus `abortedPosts`), `before/measure.json`, `before/summary.md` (A-shop-001 to 029), `tests-before.txt` (two `count:` lines), `tools/shop-behaviour.mjs`. `git status --porcelain` on QuestBoard.Service, QuestBoard.IntegrationTests and QuestBoard.UnitTests prints nothing. The baseline answers 200 on 8321.
