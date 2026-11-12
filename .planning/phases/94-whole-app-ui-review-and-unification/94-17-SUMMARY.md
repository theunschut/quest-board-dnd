---
phase: 94-whole-app-ui-review-and-unification
plan: 17
subsystem: ui-direction-signoff
tags: [g1, direction-sign-off, tokens-final, contrast-rescore, operator-answers, relayout-in-principle]
requires:
  - phase: 94-14
    provides: "94-AUDIT.md index, 138 operator questions, relayout and merge lists, area cut"
  - phase: 94-16
    provides: "G1 review package (mockups, contrast-all.json, SIGNOFF.md)"
provides:
  - "94-UI-SPEC.md: Decisions set at direction sign-off table with S1 to S12 filled and dated, plus the G1 sign-off record and open items"
  - "94-AUDIT.md: an Answer line under all 138 Q-nn, relayout list rows with real Direction A mockup PNG paths and the in-principle answer, merge rows marked carried or kept, two operator-initiated relayouts, a sign-off record"
  - "~/qb-ui-review/phase-94/signoff/tokens-final.css: the final token values (Direction A, texture strong, scrim default, wood action, Shop navy), custom properties only, every contract token name declared"
  - "~/qb-ui-review/phase-94/signoff/contrast-final.json (+ rescore.mjs): 487 pairs re-scored at B-med, B-99, B-max and B-hi, none failing"
affects: [94-18, 94-23, 94-27, quests-area-phase, shop-area-phase, events-area-phase]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-17-SUMMARY.md
  modified:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md
decisions:
  - "Direction A (refined glass) with stated exceptions, Shop variant (a) navy; texture strong, wood action fill, default 42% scrim, static phone navbar; every other S value as Direction A proposed it (the answer sheet's prefilled S2 to S11 were Direction B's and were not used)"
  - "Desktop quest board keeps the parchment poster artwork; only the information layout inside the poster is re-laid out, drawn first in the Quests phase; the slip surface is not in the final tokens"
  - "Calendar takes Direction A's look with the desktop month visible without scrolling; the +N more behaviour is confirmed by the operator against the calendar-fit mockup before the Events phase"
  - "All 138 Q-nn answered; the only hard override of the sheet is Q-128 (Shop variant (a)); all 17 relayouts approved in principle and re-confirmed with their AFTER in their area phases"
metrics:
  duration: "one continuation session (Task 3)"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 36500
  tasks: 3
  commits: 2
---

# Phase 94 Plan 17: G1 Direction Sign-Off Summary

Direction A ("refined glass", texture strong, wood action, default 42% scrim, static phone navbar) with Shop variant (a) navy and three stated exceptions is signed off; all 138 questions are answered, S1 to S12 are recorded, and the final token set is built and re-scored at 487 pairs with none failing at B-max.

## Operator resume signal (G1, 2026-11-01)

The operator answered conversationally; this is the orchestrator's transcription, treated as the resume signal:

- **S1 / Direction:** Direction A overall, recorded as "Direction A with stated exceptions".
  1. Quest board (quest overview): keep today's parchment poster images ("they really fit the quest board vibe because they are actual parchment"), not Direction A's pinned slip. What the operator dislikes is the internal layout of the information on the poster: relayout approved in principle, drawn and shown in the Quests phase before it is built. Ink on the poster measured 3.8 to 4.2:1 at the 5th percentile (fails 4.5:1 for body text), titles qualify as large text (3:1); agreed approach: keep the art and very gently lighten the text zone inside the poster so body text reaches 4.5:1 at B-max. Foundation plans must not convert quest cards to the slip treatment.
  2. Shop: variant (a), navy. The floating shop button bottom-right in the mockups hovers over the footer and needs a new place (Shop phase, required fix).
  3. Calendar: Direction A look; the desktop month view must fit without scrolling (compact one-line entries, "+N more", expand on hover, focus and tap, phones keep the agenda list, target whole month visible at 1280 by 720 and up). The "+N more" interaction is not final: the operator confirms it against the calendar-fit mockup before the Events phase.
- **S values:** texture STRONG, action WOOD, scrim DEFAULT, phone navbar STATIC, Shop (a) navy; every other value as proposed by Direction A.
- **Q-nn:** "use the answer sheet's suggestions" for all 138 except where the decisions above override (Q-128 is Shop (a)); Q-14 and Q-15 add the pickers to the mobile view.

## What was done (Task 3)

- **UI-SPEC:** every S1 to S12 row filled with the value and the date (S13 untouched); a "G1 sign-off record" block under the table holds the exceptions and open items G1-O1 to G1-O4; one sentence added to the Typography Cinzel rule for the accepted notice-title departure.
- **AUDIT:** an `Answer (G1, 2026-11-01)` line under every Q-nn (57 [Add it to the other view], 37 [Deliberate, record it], 13 merges approved and carried into their area phases, 3 kept, 17 relayouts, 11 other). The Relayout list has an Answer column; mocked pages carry the real Direction A PNG paths (all 12 distinct paths exist); every other page keeps the not-yet-drawn line; every answered relayout reads exactly `approved in principle — re-confirmed with its AFTER in the area phase`. Merge rows say approved and carried, or kept. Two operator-initiated rows were added (G1-R1 quest poster card layout, G1-R2 calendar month fit). A sign-off record closes the questions section.
- **Overrides traceable next to the Q-nn:** Q-128 (sheet said (b); operator chose (a)). Consistency notes on Q-14, Q-15, Q-21 to Q-23, Q-40 to Q-47, Q-60, Q-116, Q-120, Q-121, Q-131 and Q-134 tie the answers to the three exceptions. Q-60 stays Deliberate (the merchant button remains desktop-only) with the relocation recorded as the required Shop-phase fix.
- **Final tokens and re-score (scratch, `~/qb-ui-review/phase-94/signoff/`):** `tokens-final.css` is Direction A's `tokens.css` with the veil removed (`--qb-board-veil` transparent, texture strong), the mockup-only switcher blocks removed (scrim 42% and wood action are now the defaults), `--qb-surface-slip` removed, and the Shop theme block kept (value-identical substitution of `rgba(0,0,0,0)` by `transparent` for its veil). All 84 contract token names are declared; 11 Direction-A additions stay (`--qb-neutral*`, `--qb-on-fill`, `--qb-chrome-top` and `-bottom`, `--qb-control-bg`, `--qb-board-veil`, `--qb-vignette`, `--qb-chrome-highlight`, `--qb-board-image`). `rescore.mjs` reads `tokens-final.css`, resolves Direction A's default-state pair lists (Shop pages under the theme block) and writes `contrast-final.json`.

## Results

- **487 pairs, 0 failing at B-med, B-99 or B-max; 0 failing even at B-hi.** Lowest text 4.76 at B-max and 4.61 at B-hi, lowest large text 7.63, lowest non-text 3.44, weakest focus ring 4.51. Bootstrap channel triples match their fills in the root and in the Shop block.
- Not scored: the scrim-light, scrim-heavy and action-gold state files and the Shop variant (b) pages (17 files, not chosen).
- Excluded: the six parchment-slip pairs of the desktop Quest board (`notice-title`, `notice-desc`, `notice-meta`, `notice-meta-muted`, `notice-title-hover`, `ring-on-slip`): the signed-off direction does not use the slip. They are re-scored when the Quests-phase poster redraw exists (G1-O1).
- **Colour cap (open item G1-O4):** counted the way Direction B's write-up counted (every hex and `rgba()` literal, Bootstrap channel triples excluded; this method reproduces B's 32 and 45) the final set has **46 in `:root` and 61 with the Shop theme (cap 40: over by 6 and 21)**. Counted as distinct opaque hex values, the unit of the contract's 77-value baseline, it is **30 and 38 (inside the cap)**. The operator's choices were not changed. The smallest compliant adjustment, for the operator to confirm before the token layer ports the file: state the cap as distinct opaque hex values and list the alpha steps separately. Even removing every non-contrast-bearing tint would leave about 50, so a literal-count reading needs a cap exception, not a tweak. There is no count test in plan 94-18; the cap lives only in the UI-SPEC.

## Open items carried forward (none blocks the foundation)

1. **G1-O1, Quests phase:** poster card information relayout, drawn and signed before it is built; body-text contrast on the poster at 4.5:1 by gently lightening the text zone inside it; no slip.
2. **G1-O2, Shop phase:** the floating merchant button needs a new place (hovers over the footer).
3. **G1-O3, Events phase:** operator confirms the "+N more" behaviour (day panel on click, tap or Enter, against hover-to-open) against `~/qb-ui-review/phase-94/mockups/calendar-fit/` before that phase; I did not touch that directory and did not verify it exists yet. The "+N more" control collapses entries behind a control, an operator-requested exception to the plan's no-collapse relayout rule, limited to day cells; it is recorded as such in S12.
4. **G1-O4, colour cap counting unit** (above).
5. Porting details already recorded in the table: bars measure 57px (desktop) and 61px (phone) against the 56px offset, so the bar height is set from `--qb-nav-h`.

## Task commits

| Task | Name | Commit |
|------|------|--------|
| 1 | Assemble and validate the sign-off package (earlier session) | none: scratch only under `~/qb-ui-review/phase-94/signoff/` |
| 2 | Direction sign-off (G1) | none: operator decision |
| 3 | Record the sign-off, answer the questions and fix the final token values | 2f058ed3 |

## Deviations from Plan

None - the plan was executed as written. Two judgement calls inside Task 3, both recorded above: the six slip pairs were left out of the re-score rather than scored against a surface the operator did not choose, and the two operator-initiated relayouts (poster card layout, calendar month fit) were added to the Relayout list as G1-R1 and G1-R2 rather than as new Q-nn numbers, so the 138-question count stays intact.

## Known Stubs

None.

## Threat Flags

None. The record is in committed planning files (T-94-29); `tokens-final.css` is the single source the next plan ports and the contrast was re-run after sign-off (T-94-30).

## Self-Check

Verified: the three Task 3 checks pass (S1 to S12 have no `pending`, `contrast-final.json` has 487 pairs all passing at B-max, the relayout section has every `approved` line carrying the exact in-principle phrase and no leftover "PNG paths filled at G1"); 138 Q-nn answers present; every contract token name declared in `tokens-final.css`; the 12 mockup PNG paths in the AUDIT exist; no absolute home path in the AUDIT. STATE.md and ROADMAP.md were not touched. mockups/direction-a, direction-b, baseline, fixtures, vendor, tools and calendar-fit were not edited.

## Self-Check: PASSED
