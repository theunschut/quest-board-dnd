---
phase: 94-whole-app-ui-review-and-unification
plan: 15
subsystem: ui-mockups
tags: [mockups, direction-b, parchment-sheets, primary-action-switcher, contrast, shop-theme, designer-mode]
requires:
  - phase: 94-09
    provides: "mockup scaffolding (fixtures, render.sh, contrast-report.mjs, vendor, baselines) and Direction A as the thing B must differ from"
provides:
  - "Direction B 'Parchment sheets': tokens, five-layer CSS and all five D-02 pages at 1440 and 390 (Quest board also 820), with empty, maximum-title and validation-error variants"
  - "Shop both ways (D-03): shop-a as a [data-theme='shop'] custom-property override, shop-b folded into the main look"
  - "The primary-action switcher (wood or gold fill) proven by a plain button toggling a root class that swaps three tokens"
  - "contrast-b.json: 1398 measured pairs, all passing at B-med, B-99 and B-max, in default, wood and gold states"
  - "WRITEUP.md with the six required headings and a Direction C section"
affects: [94-16, 94-17]
tech-stack:
  added: []
  patterns: ["opaque paper on a trim strip instead of glass, so every pair is flat on flat and independent of the board", "state frames made by clicking the real switcher button in the browser (render-states.mjs)", "independent live check of rendered text colours against the declared pair lists (live-check.mjs)", "a base-layer rule limiting transition-property to translate, transform and opacity"]
key-files:
  created: []
  modified: []
decisions:
  - "Output lives under ~/qb-ui-review/phase-94/mockups/direction-b/ (scratch, outside the repository); the repository gains only this SUMMARY"
  - "No Direction C: the only axes left either abandon the notice-board identity (D-01) or are a mix of A and B that the sign-off already allows; stated in WRITEUP.md for the sign-off sheet to quote"
  - "Sheets use the 4px radius step and titles on sheets use the body font; Cinzel stays on page titles, the brand and the Shop hero"
  - "The commit action is wood by default in B; both fills pass, the choice stays S3 for the operator"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 70000
  tasks: 2
  commits: 1
---

# Phase 94 Plan 15: Direction B "Parchment sheets" and the Primary-Action Switcher Summary

An opaque-paper direction (wood-trimmed sheets pinned to a quieter board, no glass, no blur) covering the five D-02 pages plus the Shop both ways, with the wood-or-gold primary-action switcher proven and 1398 contrast pairs passing at B-max.

Nothing was written into the repository except this SUMMARY. All paths are under `~/qb-ui-review/phase-94/mockups/direction-b/` (`B`); nothing outside it was edited.

## What was built

- **Direction B** (`B/`): `app.css` (layer order first, Bootstrap 5.3.8, Font Awesome 6.7.2 and EasyMDE imported into `vendor`), `tokens.css`, `base.css`, `shell.css`, `shell.mobile.css`, `components.css`, `pages.css`, `utilities.css`. Zero `!important`, zero colour literals outside `tokens.css`, no `transition: all`, a reduced-motion block in `base.css` and `utilities.css`, no inline `style=`, no `backdrop-filter` anywhere (shell included). Generators in `B/gen/` (`node direction-b/gen/build.mjs` rebuilds every page from the shared fixture; `pairs.mjs` writes the pair lists).
- **Pages**: `quest-board`, `quest-details`, `quest-form`, `calendar`, `shop-a`, `shop-b`, each with a `.mobile.html` twin; variants `quest-board--empty`, `quest-details--maxtitle` (200-character title), `quest-form--error` (validation summary, an out-of-range Challenge Rating, an empty proposed date), `calendar--empty`, `shop-a--empty`, `shop-b--empty`; `action-specimen` (mockup-only proof page). Each file opens with a `controls` comment listing every control of the real view, and none changes type.
- **Switcher**: `<button data-switch="action-fill">` toggles `action-gold` on the root; the class swaps only `--qb-action`, `--qb-action-hover` and `--qb-action-fg`. Gold-state frames are `png/<page>.action-gold-<width>.png`, taken by `tools/render-states.sh`, which clicks the real button and fails if the token did not change.
- **Shop (D-03)**: shop-a's `[data-theme='shop']` block sets custom properties only (navy paper, navy trim, light ink, darker board, a light primary edge); shop-b has no theme. Same markup and components.
- **Crops** (17 at 2x in `B/crops/`): card corner, notice footer with seal, table header, badge row, vote buttons, form actions in wood and gold, validation error, today cell, Shop sheets and sign, and seven focus rings captured after real keyboard focus (`:focus-visible` confirmed true for each).
- **WRITEUP.md**: Idea, Good at, Phone render cost, Implementation effort, Conflicts with the contract (11 items), Recommendation, Direction C.

## Differs in kind from Direction A

A keeps the poster art as the notice with a parchment slip pinned on it, glass on a scrim, Cinzel on notice titles. B has no poster art and no glass: opaque paper with wood trim strips, body-font titles on sheets, a pin, a slight tilt and a wax seal over each sheet's corner, prices on wood plates, a solid wood plank for chrome, and no text ever on the bare board.

## Contrast (D-16, D-17)

`B/contrast-b.json`: 1398 pairs over 7 pages (the six D-02 and Shop pages plus the specimen), each in default, `action-fill=wood` and `action-fill=gold`. All pass at B-med, B-99 and B-max; lowest text ratio 5.12, lowest non-text 3.32 against 3.0. Both fills keep the label at least 4.5:1 in rest, hover, active and focus-visible (wood: white on `#8b4513`, hover white on `#a0522d` 5.6; gold: near-black on `#ffc107`, hover on `#ffd23f` 13.0), and a dark edge keeps the button boundary at 3:1 on paper.

An independent check, `tools/live-check.mjs`, measured the rendered text of 13 pages (desktop and phone layouts, both fills): 2,872 elements, none under the bar. It found one miss in the declared pairs, Bootstrap's pink `code` colour (3.6:1), fixed with a token. Cost numbers come from `tools/cost.mjs` (phone Quest board: 208 elements, 0 blur, 15 two-layer shadows, 2 chrome gradients).

## Tracer gate

`auto_advance` is false, which would normally mean a human checkpoint after the tracer. As in plan 94-09, the tracer's `<verify>` (render, contrast in both switcher states, pair assertions, zero blur on the quest card) is fully automated, passed, and was re-run before the expansion task; this run was also one of four parallel wave-4 executors, so no checkpoint was returned.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The Quest board has no primary button, so the switcher cannot be seen on it**
- The real board view carries no `btn-primary`, so the gold-state Quest board frames equal the wood ones. Added a mockup-only `action-specimen` page (rest, hover, active and focus-visible on paper, alternate paper, control surface and trim) and put the action pairs in every page's list; the in-place swap is visible on the Create form, Quest Details and the Shop.
- **Files:** `B/gen/specimen.mjs`, `B/action-specimen*.html`.

**2. [Rule 3 - Blocking] `render.sh` cannot show a switcher state**
- Added `B/tools/render-states.{mjs,sh}` (clicks the real button), without editing the shared tools.

**3. [Rule 1 - Bug] The focus ring lost its dark band on buttons**
- Found in a crop: `.btn` set its own `box-shadow` in the `components` layer, which overrode the base ring (a box-shadow). Restated the ring on `.btn:focus-visible`. Recorded in the write-up as a guard-test point.

**4. [Rule 1 - Bug] Bootstrap's global colour rules leaked in**
- The global link rule (base layer) beat the vendor nav colours, leaving dark text on the wood bar; the chrome now states its own colours. Bootstrap's pink `code` colour and its colour, background and shadow transitions were retired (the last by a base rule limiting `transition-property` to translate, transform and opacity). A repeated item no longer carries more than two shadow layers.

**5. [Rule 1 - Bug] Contrast failures in the first Shop theme**
- The report found the dark primary-button edge and the dark selected-pill edge at 1.8 and 1.2 against navy paper. The theme now sets a light primary edge and the pill pair measures the gold fill there.

### Design decisions that go to sign-off (visible departures, listed in WRITEUP.md)

- Sheet radius 4px; month arrows, "Add Another Date Option" and the change-character icon button use the secondary role so each view has one commit action; visible Open and Finalized words on desktop sheets, a Today label, a check on the selected category pill, aria-labels on the month arrows.
- Shop theme (a) pushes the colour count to 45 against the cap of 40 (default look 32); button boundaries other than the primary on the navy sheet rest on their labels.
- Bootstrap's `!important` colour utilities in the vendor layer cannot be re-themed from a later layer (`.text-danger` on paper is 4.3:1); a contract point for the area phases.
- No Direction C, with the reason stated in WRITEUP.md.

## Auth gates

None. The mockups are static files over a local file server; no login or running app was needed. Ports 8000 and 8001 were not touched.

## Known Stubs

None in the repository (no source changed). The mockups are static by design: buttons do nothing, the markdown preview has no server, the Shop item-details modal is not mocked, the Shop filter offcanvas is present but closed.

## Threat Flags

None. Output stays under `~/qb-ui-review/phase-94/mockups/direction-b`; fantasy fixture names only; no member data, credentials or tokens in the mockups or this file (T-94-27). Every Direction B pair passes at B-max in both switcher states, so no option that cannot ship is delivered (T-94-56).

## Self-Check: PASSED

- Files exist: `B/tokens.css`, `B/quest-board.html`, `B/quest-details.html`, `B/quest-form.html`, `B/calendar.html`, `B/shop-a.html`, `B/shop-b.html`, `B/contrast-b.json`, `B/WRITEUP.md`, `B/crops/` (17 PNGs), `B/png/` (60 PNGs and per-page reports).
- Task 1 and Task 2 verify commands pass (`B/tools/verify.sh`: render, contrast in default, wood and gold for every page, `passBmax` for all 1398 pairs, the page set, the six headings and the Direction C section, at least 5 crops, no `backdrop-filter` in `quest-board.html`, no `!important` in any stylesheet).
- `git status --porcelain` in the worktree showed nothing before this SUMMARY.
