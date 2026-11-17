---
phase: 99-shop-and-shop-management-ui-conversion
plan: 19
subsystem: shop-ui
tags: [shop, closing-sweep, accessibility, click-through, migrator-suite, gate-2]
requires: ["99-18", "99-21"]
provides:
  - the full AFTER of every manifest row under both User-Agents in ~/qb-ui-review/phase-99/after/final (140 records, 0 regressions against BEFORE)
  - Return, Sell and Sell to Shop clicked through for real on both layouts, recorded in clicks.json
  - a11y.md (A-1 to A-11 per page and layout), findings-trace.json (A-shop-001 to A-shop-029), contrast-exceptions.json entries for Gate 2
  - deferred-items.md and the WINDOWS ledger rows (12, 22, 28 closed for the Shop, entry 33 added)
affects: [99-20]
tech-stack:
  added: []
  patterns: [real form clicks as the fixture player with the confirm sentence recorded, a probe that holds the POST back to show the in-flight state, text-hidden element screenshots to score contrast at the lightest pixel]
key-files:
  created:
    - .planning/phases/99-shop-and-shop-management-ui-conversion/deferred-items.md
  modified:
    - .planning/WINDOWS.md
decisions:
  - "No repository source changed: the sweep measures and records; every measured difference that is not a regression is an entry in contrast-exceptions.json pending Gate 2."
  - "Sell is clicked on a day-0 purchase that is visible in the newest 15 history rows; Return only on purchases made in this plan, so the click-throughs never touched a purchase the sweep needed unreturned."
  - "The submit lock finding (no shop form opts in to data-disable-on-submit, at the baseline or now) is recorded as a decision and a ledger entry, not fixed here."
metrics:
  duration: "about 45 minutes of wall clock (sequential single pass)"
  completed: 2026-11-17
status: complete
actuals:
  tokens: 6800
  tasks: 3
  commits: 1
---

# Phase 99 Plan 19: The closing sweep Summary

The finished shop area was captured once, completely, under both User-Agents with the Sell state the day-0 purchases make possible, clicked through for real (Return, Sell, Sell to Shop on desktop and phone), measured against the accessibility bar, and left with every exception and decision as a pending Gate 2 entry; no repository source changed.

## Click-throughs (real posts through the app's own forms, p99player on P99 Audit Shop)

The Sell state was reached on the calendar: `fixtures.mjs --sell-status` reported `eligible: true` (eligible since 2026-11-17T11:17:54Z, 18 aging purchases). Nothing was backdated; no SQL.

| Click | Layout | Landed | Confirm sentence | Result |
|---|---|---|---|---|
| Return (newest fresh purchase) | desktop | yes, 302 | none (Return does not confirm) | Wizard's "Staff" row became "(Fully Returned/Sold)" |
| Return | phone drawer | yes, 302 | none | Ember-Forged Traveler's Pack row became "(Fully Returned/Sold)" |
| Sell (aging purchase, visible row) | desktop | yes, 302 | "Are you sure you want to sell 1x Ember-Forged Traveler's Pack to the shop for 13 gp?" | the row became "(Fully Returned/Sold)" |
| Sell | phone drawer | yes, 302 | "Are you sure you want to sell 3x Ceremonial Gauntlets of the Thrice-Crowned Warden of Highmere Reach to the shop for 675 gp?" | the row became "(Fully Returned/Sold)" |
| Sell to Shop, dialog, quantity 2 | desktop and phone | yes, 302 to Details | "Are you sure you want to sell 2x Ember-Forged Traveler's Pack to the shop for 25 gp?", total 25 | success toast "You sold 2x Ember-Forged Traveler's Pack to the shop" with "Gold Received! +25 gp" |
| Sell to Shop, Details page, quantity 2 | desktop and phone | yes, 302 to Details | the same sentence | the same toast, on `/Shop/Details/5448` |

All eight are in `after/final/clicks.json` (`return-`, `sell-`, `selltoshop-dialog-`, `selltoshop-page-` with `-desktop` and `-mobile`), with the per-click records beside it. The history lists purchases only (the service returns Purchase rows), so a Sell to Shop leaves no history row; its proof is the 302 to Details and the toast on the landed page. After a Return or a Sell the history labels the row "(Fully Returned/Sold)" for both, so "a Sell-type row" in the history is the sold aging purchase.

The captured frame with a Return row, a Sell row, fully returned rows and the sold row together is `after/final/desktop/shop-history-sell.png` (desktop pane open) and `after/final/mobile/shop-index-history-phone--w390.png` (phone drawer); `after/final/tracer/desktop/shop-history-sell.png` is the pre-click frame, and `after/final/after-clicks/` the state after all eight clicks.

**Purchases made in this plan: 2** through the Purchase form (`fixtures.mjs --fresh-purchases`): Wizard's "Staff" x1 (500 gp) and Ember-Forged Traveler's Pack x1 (25 gp). Both were returned (one per layout). **Sold:** two day-0 purchases (Ember-Forged x1 and Ceremonial Gauntlets x3, one per layout). **Sell to Shop:** four recorded posts of 2x Ember-Forged Traveler's Pack (the item is unlimited, so stock is unchanged), plus up to two unrecorded ones from a first run of the click tool whose response filter had the wrong path (the tool was fixed and re-run; those two posts probably reached the server and cannot be told apart in the history). No purchase was refused for lack of funds; the app has no gold or balance.

## The sweep

- `sweep.mjs --pages phase` (all 38 manifest rows, afterOnly included) into `after/final/`: 140 records, all 200, no console error, page error, failed request, failed action or failed assert, no horizontal overflow, 0 focus-ring-missing. `report.json` screenshot paths point into `after/final/`.
- `readability-compare.py` against BEFORE (started from the merged `after/99-18/allow.json`, `--pages` omitted as the carry-forward says): 129 compared, 0 failing; with `--zero-contrast phase`: 0 residual contrast rows (`compare1.txt`, `compare2.txt`).
- Seven records carry a small-target or text-spacing flag; all are present in BEFORE unchanged (the phone editor's 24 px Description region on Create; the 1 px text-spacing overshoot of the three desktop Edit rows at 320). See the exceptions below.
- Guard facts first: `ShopConversionGuardTests` 49 passed before any capture.

## Accessibility bar (after/final/a11y.md)

Per page and layout: in-main contrast 0 failing of the pairs tested (BEFORE 1347); focus ring missing 0; focus obscured 3 (the editor's hidden source textarea under its own preview, inherited); phone targets under 44 px: 4 records, all the inherited editor region; desktop targets under 24 px beyond BEFORE: the merchant toast Close (21 px, as BEFORE) and the editor textarea (inherited); no horizontal scroll at any width; text-spacing overflow only on the three Edit rows at 320 (as BEFORE); reduced motion: 0 running animations and 0 transitions over 1 ms on every row; infinite animations 0 (the merchant bob is one capped, 3-iteration animation, still under reduce); exactly one `h1` on every page and layout; 0 unnamed controls; no page-written toast (the 11 `role="alert"` hits are the framework's empty validation span under the Description editor).

- Focus frames (`focus-frames/`, 28 frames): tabs, filter checkboxes, cards through `:has`, Purchase, pager links, Filter Apply, My Transactions toggle, slip buttons, merchant, dialog Close, dialog Purchase, Back to Shop, management table icon buttons, management bar, form submit; every one has a ring and none is clipped.
- Lightest-pixel contrast (`pixel-check.json`, 37 measured pairs; 4 phone cases name elements the phone layout does not have): every new pair passes, lowest 5.63:1 (management table header) and 6.09:1 (rarity chips); the required marker on Create and Edit measures 9.3:1 to 9.5:1. (The My Transactions label is scored against its own button and its count pill against the pill's fill; the first run scored them against each other and was corrected, the artefact is not a finding.)
- Whole image (`whole-*.json`, 32 records: long-name and wide-image rows and dialogs, both layouts, with and without the text-spacing stress): every glyph inside its tile, every image no wider than its container, no tile or image with an aspect ratio, object fit or hidden overflow.
- Submit in flight (`inflight-99-19.json`): the POST was held back and the primary button stayed enabled with its label (Purchase Item 6.46:1 desktop, 6.27:1 phone; Save Changes the same). See decision `decision-99-no-submit-lock` below.
- Merchant (`merchant.json`): keyboard reach, name, 56 px, polite live region equal to the toast, one toast at a time, held on hover and released after leave, clear of the footer at 1440 and 1024, still under reduced motion. `index-keyboard.json` (card ring, Enter opens, Escape returns focus, latest wins, dialog fits with the long-name and wide-image items) and `phone-probe.json` (columns 1/2/2 at 320/360/390, no overflow, no small target, both drawers trapped with focus returned, dialog 828 px in an 844 px window with the body scrolling) all pass. `behaviour.json`: the Sell to Shop confirm in the dialog and on the page, Preview opens its dialog (14 elements added, no page error), View Details answers 200, and the Logout button and the item form post without any page-written intercept (the old inline Logout handler is gone).
- Look check (`lookcheck.json`, 32 frames against Direction A): all desktop frames pass with only the approved intended differences; the phone frames differ on two roles as listed under the decisions.
- Unmoved pages (`unmoved.json`, 31 computed properties plus box size over every element): the quest board (`/`, which resolves to `/quests`) 80 elements desktop and 75 phone, Calendar 316 and 112, against the frozen baseline on 8321: 0 differences.
- Editor toolbar on Create and Edit under the phone User-Agent (`editor-toolbar.json`): every button is 44 by 44 px (7 buttons per editor, all three pages, both User-Agents), so the 30 to 34 px figure of the UI contract no longer reproduces; reported as the inherited figure of the shared editor, not as an exception of this phase.

## Finding trace

`findings-trace.json` holds A-shop-001 to A-shop-029: **28 closed, 0 exception, 1 carried** (A-shop-010, fixed separately before the phase and proven by 99-02), each with severity, owning plan, BEFORE text and AFTER evidence. Owning plans come from the plans that name each finding (99-04, 99-08, 99-10 to 99-17).

## Exceptions and decisions pending Gate 2 (all `approved.status: pending`, none treated as approved)

Entries in `~/qb-ui-review/phase-99/contrast-exceptions.json`:

1. `exception-99-toast-close-21px`: the merchant toast's Close button is 21 by 21 px on desktop (21 px BEFORE too; the framework close icon).
2. `exception-99-editor-inherited`: the shared editor's hidden textarea (16 px high, under its preview on Tab) and the phone's 24 px Description region; both present before the phase.
3. `exception-99-text-spacing-320-edit`: 1 px overshoot (321 against 320) on the three desktop Edit rows at 320 under the full text-spacing stress; the same in BEFORE.
4. `decision-99-no-submit-lock`: no shop form opts in to the site.js submit lock, so there is no lock to keep and no disabled look to measure (also WINDOWS entry 33, and a row in deferred-items.md).
5. `decision-99-look-phone-drawer`: on every phone look frame the history card's panel (glass fill, border, radius, shadow) and the title band's right padding (60 px against 24 px) differ from the render, because the approved drawer is the surface and the Close button sits over the title row.
6. `decision-99-look-phone-action-row`: the phone form action row aligns items to stretch (the foundation row) against the render's center.
7. `decision-99-unmoved-sample`: the p99 board has no quests, so the quest-board proof covers the page chrome and empty state (99-18's with and without comparison covers the wider set).

No fixture state was recorded unavailable. Gate 1 decisions that are still open: none (every item in `gate1/answers.json` is answered, item 20 as "fix in phase 99").

## Migrator suite

`QUESTBOARD_MIGRATOR_TEST_REQUIRED=1 dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Migrator"` with the connection string from `migrator.env` loaded into that one command's environment only (`operator-answers.json` says `yes`): **11 passed, 0 failed, 0 skipped**. The connection string was never printed or written; `grep -c migrator after/final/a11y.md` prints 0 and no record under `after/final` contains `Password=`.

## Tests

- Unit tests 1433 passed.
- Integration tests 2568 passed, 16 skipped (the SQL Server tests the migrator filter runs separately), 2584 total.
- Migrator suite 11 passed.

## Ledger and deferred items

`.planning/WINDOWS.md`: entries 12 (required marker, Shop rows closed with the 9.3 to 9.5:1 figures), 22 (the shop theme on `<html>`, the photo behind `--qb-board-photo`) and 28 (shop.css's 70 important flags deleted, legacy-pages.css's 49 gone, the three layered sheets carry none, phase counts 152 to 0) now carry the Shop closure; their statuses are unchanged (12 and 22 already fixed; 28 stays open for sheets other phases own). The table and the JSON block it is rendered from were both updated. Entry 33 (open, unmet-truth) records the missing submit lock. `deferred-items.md` records server quantity validation as **Resolved in this phase** (99-21, commit `5b77dddc`, Gate 1 item 20) and defers: a Draft item's Details opened by any member, Markdown images as a possible tracking pixel, dialog dates keeping the server wording until reload, the missing submit lock, the stale "DM profile pages" guard message raised by 99-18, and the inherited editor and toast-close figures.

## Fixtures and state

- Fixture note from 99-17: item 5485 "Ember-Forged Traveler's Pack" (p99dm, board P99 Audit Shop) is the stray duplicate created by a `fixtures.mjs` run without a step flag; it is Archived and was not touched again. `fixtures.mjs` was run only with `--sell-status` and `--fresh-purchases`.
- The permission classifier did not refuse the purchase command this time (operator authorisation in the session).
- Port 8320 (the working tree) and port 8321 (the frozen baseline, restarted only for the unmoved-pages comparison) are stopped; no shell, probe or app this plan started is still running.

## Deviations from Plan

**1. [Process] Task 1's capture went to `after/final/tracer/`.** `sweep.mjs` overwrites `report.json`, so the two-row pre-click capture went into a sub-folder and the full sweep into `after/final/`; the verify only reads `clicks.json`. (Rule 3)

**2. [Tool] Copies made for this plan** (scratch, outside the repository): `history-click-99-19.mjs` (a Sell may land on a purchase older than 24 hours; Return is still refused on any day-0 purchase), `selltoshop-click-99-19.mjs` (real Sell to Shop posts; its first version filtered the response by an exact path and timed out, twice, on the desktop dialog and page, after the form had probably posted; fixed and re-run), `build-clicks-99-19.py`, `focus-frames-99-19.mjs`, `pixel-check-99-19.mjs`, `submit-inflight-99-19.mjs`, `editor-toolbar-99-19.mjs`, `shop-behaviour-99-19.mjs` (the merchant selectors of the old script point at removed classes), `unmoved-99-19.mjs`, `a11y-table-99-19.py` and `findings-trace-99-19.py`. `look-spec-99.json` gained one alternative selector for the management title card in live mode (backup `look-spec-99.json.pre-99-19`); the mock-only wrapper it named does not exist on the real page. (Rule 3)

**3. [Gate 1 redraw] History checks adapted to the approved pane and drawer** (the clicks open the pane first; the visible history is the newest 15 rows, and the Sell row clicked is one of them), as the Gate 1 overrides direct. The filter card is full width and the phone tabs live in the filter drawer; the focus frames follow that.

**4. [Order] The full suites and the migrator suite ran before the sweep** instead of after it, because no source changes in this plan and the app has to be stopped for any build; the counts are unchanged by the sweep. (Rule 3)

**5. [Ledger format] WINDOWS.md is rendered from the JSON block under the table**; the closure text was written to both, and `windows append` re-rendered the table, so a table-only edit would have been lost.

**6. [Route] The quest board is `/`**, not `/Quest` (404); the unmoved comparison used `/`, `/Quests` and `/Calendar`.

Otherwise none; the plan executed as written (no source change, no backdating, no SQL).

## Known Stubs

None.

## Threat Flags

None. T-99-50 (migrator string): loaded into one command, never echoed or written; the greps above prove it. T-99-51: every write was the p99 player's own purchases on P99 Audit Shop through the app's forms. T-99-52: no backdating; the calendar had passed the eligible time.

## Self-Check: PASSED

- `after/final/report.json`, `clicks.json`, `a11y.md`, `findings-trace.json`, `contrast-exceptions.json`, `deferred-items.md` and the WINDOWS rows exist; the Task 1 and Task 2 verifies printed `ok`; the migrator suite is recorded.
