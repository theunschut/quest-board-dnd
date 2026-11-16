---
phase: 99-shop-and-shop-management-ui-conversion
plan: 07
subsystem: ui
tags: [ui-review, gate-1, operator-approval, shop, answers]
requires:
  - phase: 99-05
    provides: Gate 1 package A frames (history, filter card, merchant, look check)
  - phase: 99-06
    provides: Gate 1 package B frames (emblem, Preview dialog, look check, proposals)
provides:
  - Gate 1 sheet v2 at ~/qb-ui-review/phase-99/gate1/ approved by the operator, with answers.json holding the build contract of the later slices
  - v2 redraw of the purchase history (collapsible side pane) and the filter card (full-width)
affects: [99-08, 99-10, 99-11, 99-12, 99-13, 99-14, 99-15, 99-16, 99-17, 99-21]
actuals:
  tokens: 3200
  tasks: 3
  commits: 1
key-files:
  created: []
  modified:
    - .planning/phases/99-shop-and-shop-management-ui-conversion/99-CONTEXT.md
key-decisions:
  - "Merchant placement (a): the Mystical Merchant stays a floating button in the sticky dock"
  - "Legendary chip is amber (--qb-warning on --qb-warning-fg, 8.92:1), as the signed-off render draws it; D-09's word gold is not followed and the ui-guidelines Gold is reserved list is unchanged"
  - "Item 20: server quantity validation is fixed in phase 99 by plan 99-21, not deferred"
status: complete
---

# Phase 99 Plan 07: Gate 1 Summary

Gate 1 was held and approved: the operator reviewed the sheet, asked for a redraw of the purchase history and the filter card, approved the v2 redraw, picked the floating-button merchant, chose the amber legendary chip and answered items 1 to 22; the answers are in `~/qb-ui-review/phase-99/gate1/answers.json`.

No repository source file was created or changed by this plan. Tasks 1 and 2 (building the sheet and passing the hygiene check) produced only scratch files under `~/qb-ui-review/phase-99/`; the only commit is the docs commit that carries this SUMMARY, the D-09 answer line in `99-CONTEXT.md`, STATE.md and ROADMAP.md. `gate-hygiene.py gate1` passes with `answers.json` in the folder (176 JPEGs, entries `README.md`, `answers.json`, `img`, `index.html`, `items.md`).

## Gate 1 answers

Source "operator" is the operator's own word; "operator (accepted recommendation)" is "Yes, as recommended" to the default recommendation; no answer was recorded by default.

| # | Item | Answer | Source |
|---|------|--------|--------|
| 1 | Purchase history (Q-120, D-10) | 1:ok, as redrawn in v2: a collapsible My Transactions side pane, collapsed by default, a drawer on the phone | operator |
| 2 | One filter card (Q-121, Q-61) | 2:ok, as redrawn in v2: a full-width filter card | operator |
| 3 | Merchant placement (D-01) | a: floating button in the sticky dock | operator |
| 4 | Preview dialog; Preview on Edit (D-03) | 4:no-edit: as drawn, no Preview on Edit | operator |
| 5 | Emblem tile and icon map (D-07) | 5:default: 144px desktop, 120px phone, existing icons | operator (accepted recommendation) |
| 6 | Look check; phone navy, offcanvas tabs, table header | 6:navy,offcanvas,wood | operator (accepted recommendation) |
| 7 | Bulk Actions | 7:keep: keep and restyle; phone modal kept | operator (accepted recommendation) |
| 8 | Sell row confirmation | 8:confirm (Return none) | operator (accepted recommendation) |
| 9 | Desktop client validation alerts | 9:no (no change) | operator |
| 10 | Denied and Archived notice on Details | 10:show | operator |
| 11 | Availability dates Nov 5 | 11:accept | operator (accepted recommendation) |
| 12 | Phone Details shared partial; dialog Back to Shop | 12:shared,keep-back | operator (accepted recommendation) |
| 13 | Management headings; empty-state button role | 13:hidden,secondary | operator (accepted recommendation) |
| 14 | Dialog load-error line | 14:no (no change) | operator |
| 15 | No gold (informational) | 15:ack | operator (accepted recommendation) |
| 16 | Legendary chip colour (D-09) | 16:amber; the ui-guidelines Gold is reserved list is NOT changed (that list applies only on gold) | operator |
| 17 | Section title Items Awaiting Review | 17:keep | operator (accepted recommendation) |
| 18 | Required markers | 18:accept | operator (accepted recommendation) |
| 19 | Still glow; filled secondary buttons | 19:glow,filled | operator (accepted recommendation) |
| 20 | Server quantity validation | 20:fix in phase 99, by plan 99-21 (added in commit b4c8b844); recorded as fixed in answers.json, not deferred | operator |
| 21 | Back to Shop Management label | 21:no (no change) | operator |
| 22 | Keep Item Pending label | 22:no (no change) | operator |

Extra decisions found while drawing: A1 gold, A2 tall, A3 beside, A4 card, A5 gold, A6 accept, A7 accept, A8 stack, A9 hide-chip, A10 ok, A11 Deny-Item, A12 ok, A14 foundation-ring, A15 44px, A16 ok (all operator, at the recommended value).

Redraw sub-answers for items 1 and 2 (the first option of each, nothing changed by the operator): the My Transactions toggle has its own row under the hero, at the start; it is collapsed on every load and the state is not remembered; on the phone My Transactions sits stacked above Filter & Sort; the count is the listed rows (at most 15) and is omitted when the history is empty.

Two new intended differences from the Direction A render were named on the sheet and are approved: `history-collapsible-pane` and `filter-card-full-width`.

## Drawings approved

| Drawing | Path | Version |
|---------|------|---------|
| Purchase history, desktop and phone (item 1) | `~/qb-ui-review/phase-99/mockups/v2/` (`history-r2-*` frames) | v2 |
| Filter card, desktop and phone (item 2) | `~/qb-ui-review/phase-99/mockups/v2/` (`filter-*` frames, `r2/`) | v2 |
| Merchant placements | `~/qb-ui-review/phase-99/mockups/v1/merchant/` | v1 (placement a chosen) |
| Preview dialog (four states) | `~/qb-ui-review/phase-99/mockups/v1/preview/` | v1 |
| Emblem tile | `~/qb-ui-review/phase-99/mockups/v1/emblem/` | v1 |
| Look check, Details, management list, management form | `~/qb-ui-review/phase-99/mockups/v1/look-*/` | v1 |
| Proposals and the table header alternative | `~/qb-ui-review/phase-99/mockups/v1/proposals/` | v1 |
| Gate 1 sheet | `~/qb-ui-review/phase-99/gate1/index.html` | v2 |

Later slices must use the v2 drafts for items 1 and 2: `~/qb-ui-review/phase-99/mockups/v2/site/shop-pages.css`, `shop-pages.mobile.css` and `mock-history.js`. The v1 sheets are superseded for those two areas; every other area uses the v1 drafts (for example `mockups/v1/site/shop-form.css`). The same pointer is in `answers.json` under `drafts` so the 99-15 to 99-17 executors find it.

## Merchant placement

(a): the Mystical Merchant stays a floating button held in a sticky dock that rests clear of the footer (32px at 1440). The operator said they want the merchant to stay the floating button. The merchant slice (99-16) can proceed; `answers.json` has `merchant.placement` = `a`.

## Deviations from the UI-SPEC defaults

Items 1, 2 and 16 had no default in this plan set (the UI-SPEC defaults them to as drawn, as drawn and amber) and each was answered explicitly.

- Item 1 and item 2: not accepted as drawn at first sight. The operator found the filters "jammed to the left" and was unsure about the transactions placement, and proposed a collapsible pane; both were redrawn as v2 and then approved.
- Item 16: D-09 is locked and names the legendary chip gold, while the signed-off Direction A render draws it amber. The operator settled it with amber, the UI-SPEC default. The D-09 answer line is recorded in `99-CONTEXT.md`. The legendary chip uses `--qb-warning` on `--qb-warning-fg` (8.92:1); the gold exception to the ui-guidelines Gold is reserved list is not created and the list is unchanged.
- Item 10: the operator chose 10:show (the UI-SPEC default is none; the notice only appears on an explicit yes, which was given).
- Item 20: the plan's task text offers 20:defer or a stop for a small added plan; the operator chose 20:fix and plan 99-21 was added (commit b4c8b844). 99-19 and 99-20 moved to waves 20 and 21, and the ROADMAP shows 21 plans. `answers.json` records `quantityValidation` as `fixed: true, deferred: false, plan: 99-21`, which 99-21's precondition reads.

## Planner assumptions

The UI-SPEC planner assumptions 1 to 10 and this plan set's assumptions P1 to P8 are all confirmed by the operator; none was contradicted.

## Redraw history

- Round 1 (sheet v1): the operator liked most of it, wanted the merchant to stay a floating button, found the filters ugly (jammed to the left) and the transactions placement uncertain, and asked for a collapsible pane, default collapsed, in a similar location as before. Result: items 1 and 2 redrawn as `mockups/v2/` (history `history-r2-*` frames, filter card `r2/`), `site/` re-copied from the current tree with only `shop-pages.css` and `shop-pages.mobile.css` changed and `mock-history.js` added; `PROVENANCE.txt` updated; mock-scan, whole-picture and look checks re-run on the redrawn frames; the sheet rebuilt as v2 and `gate-hygiene.py` re-run.
- Round 2 (sheet v2): the operator approved. The four redraw sub-questions kept their first option.

## Deviations from Plan

None - the plan was executed as written. The only departures are operator decisions, listed above (item 20 fix; the D-09 colour answer).

## Known Stubs

None. This plan changed no repository source file.

## Self-Check: PASSED

- `~/qb-ui-review/phase-99/gate1/answers.json` exists with every key the Task 3 acceptance criteria name, plus `drafts`, `extraDecisions` and `intendedDifferencesApproved`.
- `99-CONTEXT.md` carries the item 16 answer line under D-09.
- The docs commit hash is recorded in the completion report.
