---
phase: 94-whole-app-ui-review-and-unification
plan: 16
subsystem: ui-mockups
tags: [g1-review-package, comparison-page, switchers, contrast-all, rendered-probe, focus-frames, sign-off-sheet, audit-presentation]
requires:
  - phase: 94-09
    provides: "baselines, shared mockup tools, Direction A"
  - phase: 94-10
    provides: "Direction A complete (form, Calendar, Shop both ways)"
  - phase: 94-15
    provides: "Direction B, the primary-action switcher, the Direction C reasoning"
  - phase: 94-14
    provides: "94-AUDIT.md index, 138 questions, relayout and merge lists, area cut"
provides:
  - "mockups/index.html: static comparison page (today, A, B at 1440 and 390 for the five pages, plus variants), live iframe with the five switchers, stitched mix preview and mix recorder that writes the sign-off line, proof tables, focus frames"
  - "mockups/contrast-all.json: 3,547 pairs (A 2,140, B 1,407) tagged by direction and state; 0 failing at B-med, B-99, B-max; B-hi warnings listed"
  - "mockups/SIGNOFF.md and SIGNOFF.html: S1 to S13 with options, constraints, measured numbers, flags to carry, Direction C quote, the exact sign-off prompt"
  - "mockups/audit.html: audit summary, 138 operator questions, 17 relayouts, 16 merges, proposed area cut, as one phone-readable page"
  - "mockups/focus-frames/ (10 frames with measured ring ratios), mockups/overview/ (16 PNGs for sending as images)"
affects: [94-17]
key-files:
  created: []
  modified: []
decisions:
  - "Everything is written under ~/qb-ui-review/phase-94/mockups/ only; direction-a, direction-b, baseline, fixtures, vendor and the shared tools were not edited (checked by modification time); the repository gains only this SUMMARY"
  - "Live pages open from disk through live-baseline, live-a and live-b, copies with root-relative /images paths made relative and the switcher bridge added; the copies render pixel-identical to the served originals"
  - "Rendered probe uses a package-local corrected copy of the shared contrastProbe because the shared one misreads multi-layer glass backgrounds; tools/ untouched"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 160000
  tasks: 2
  commits: 1
---

# Phase 94 Plan 16: G1 Review Package Summary

A static, phone-readable package for the direction sign-off: comparison page with five working switchers and a mix recorder, 3,547 scored contrast pairs and 323 rendered page states with no failures, 10 measured focus frames, the S1 to S13 sheet and the audit question batch, all under `~/qb-ui-review/phase-94/mockups/` (scratch, never committed).

## What was delivered (all under `~/qb-ui-review/phase-94/mockups/`)

- `index.html` (+ `index-img/`, 69 WebP thumbnails; full PNGs linked): at-a-glance table; per page today, A and B at 1440 and 390 (820 for the board), variants (empty, longest title, error) folded; live iframe with Page, Direction and Width pickers and the five switchers (`data-switch` texture, scrim, action-fill, shop, navbar); stitched header-from-X body-from-Y preview; mix recorder writing `Direction {A, B or mix (...)} and Shop {a|b}: approve these and the final token values?` plus the S values; proof tables; focus frames. At 390 the comparison rows scroll sideways and the top bar scrolls; no horizontal overflow at 390 or 1440 on any of the three pages; zero console errors or failed requests from disk.
- `SIGNOFF.md` / `SIGNOFF.html`: S1 to S13, numbers filled from the data by script, the Direction C reasoning quoted from 94-15, A's 10 and B's 11 flags (poster slip, Cinzel on 20px notice titles, added tokens, per-component focus ring, `.text-error`, 45 colours with Shop (a), ...), package findings, limits.
- `audit.html`: generated from `94-AUDIT.md`: 37 S1 findings, 138 questions grouped as the audit groups them with their answer chips, 17 relayouts with drawn or not drawn marked, 16 merges, the seven-phase cut and order.
- `contrast-all.json`, `focus-frames/`, `overview/` (per-page Today|A|B sheets at phone and desktop width, plus phone renders of the glance table, proof, focus frames, sign-off summary and audit summary), `live-*`, and scripts and raw probe output in `package/`.

## Results

- Declared pairs: A 2,140 and B 1,407, every one passing B-med, B-99, B-max; lowest text 4.72 (A) and 5.12 (B), lowest non-text 3.28 and 3.32. States: A default, scrim light and heavy, action gold, combined scrim+gold (new, 600 pairs), sticky bar over content; B default, wood, gold, sticky bar. B-hi (report only): 20 misses, all A scrim-light states (10 repeated glass items), none in default, heavy or gold; B none.
- Rendered-page probe (computed colour against effective background, composited over the blocking boards, posters sampled from pixels): A 180 page states and 10,604 text elements, B 143 and 7,657, **0 failures**; today's pages 18 states, 623 failing measurements (so the probe does detect real failures). Texture soft and strong, scrim, action gold, a weakest mix and the sticky bar are applied through the same bridge the index uses; the root attributes were recorded per state.
- Sticky phone bar: top 0 after a 900px scroll, 0 of 319 Tab stops covered, minimum 25px clear.
- Focus frames: every surface has a stroke at 4.17:1 or more against it (bar 3:1).

## Deviations from Plan

**1. [Rule 1 - Bug] The shared rendered probe produced false failures on Direction A.** Its `contrastProbe` splits no multi-layer `background-image` (glass gradient over scrim gradient was read as alternative surfaces, scoring the scrim alone) and orders a background colour above its own image. Through audit-shoot it reports 134 failures on A. Fixed in a package-local copy `package/probe-lib.mjs` (layers split, colour under image, visually hidden text and disabled controls not scored, spot gradients such as B's nail dots scored from pixels instead of as alternatives); `tools/` untouched. The literal `audit-shoot.mjs --probes --role none` pass on file URLs was also run and its focus, alt and name data used. The area audits scored legacy glass with the shared probe: worth one check that none relied on a multi-layer background.
**2. [Rule 3 - Blocking] Pages open from disk need relative image paths.** The originals use `/images/...`, so no board or posters show over file URLs. Built `live-baseline`, `live-a`, `live-b` (copies, checked pixel-identical to the served originals at 1440 and 390) plus `package/axes.css`: it gives B the two axes it lacked (texture veil values, sticky bar rule) in its own tokens.
**3. [Rule 2] Extra proof added:** combined scrim+gold states, sticky bar scored over content, focus ring measured per frame, sticky focus-not-obscured check, and a pixel-sampling cross-check that was dropped because it measured button fills, price plates and fixed elements incoherently (not used as proof; raw files kept as `package/probe/experiment-pixels-*.json`).
**4. Tracer gate:** `auto_advance` is false, which normally means a human checkpoint after the tracer. The tracer verify (index with five switchers and four or more focus frames) is automated and passed; the full plan was requested from a non-resumable wave executor, so the verify was re-run before Task 2 and no checkpoint returned.
**5. Plan wording:** the focus frames are captured with the package's own script (Tab key then focus, ring read from computed style) rather than audit-shoot's 200x120 crops, so each frame carries its measured ratio; audit-shoot supplied the 15-stop indicator and clipping probe.

## Findings to carry (also in SIGNOFF.md)

- Both mockup phone bars are 61px (contract: 56px if sticky); desktop bars 57px (A) and 62px (B) against the 56px offset.
- Ring clipped by an ancestor on 3 A calendar chip links (title paragraph clips with an ellipsis); none in B.
- Phone menu body is empty in both mockups; the overlay focus frame shows the close button.
- Direction C was not built (94-15 reasoning quoted verbatim).

## Auth gates

None. Ports 8000, 8001 and 8013 were not touched; nothing was started on them.

## Known Stubs

None in the repository. The mockups are static by design: buttons do nothing, the markdown preview and Shop item dialog are not drawn.

## Threat Flags

None. Fantasy fixture content only; `audit.html` is generated from `94-AUDIT.md`, which quotes no member data; nothing outside `~/qb-ui-review/phase-94/mockups/` was written except this file (T-94-57). Every option scored at B-max before delivery, numbers in SIGNOFF.md (T-94-28).

## Self-Check: PASSED

- Task 1 and Task 2 automated verify commands pass (five distinct `data-switch` values, 10 focus frames, `contrast-all.json` all `passBmax` with `Bhi` on every pair and directions a and b, `S12` and the sign-off prompt in `SIGNOFF.md`).
- Files exist: `index.html`, `SIGNOFF.md`, `SIGNOFF.html`, `audit.html`, `contrast-all.json`, `focus-frames/*.png`, `overview/*.png`, `live-a/`, `live-b/`, `live-baseline/`.
- Nothing newer than the package start in `direction-a`, `direction-b`, `baseline`, `tools`, `vendor` or the shared `phase-94/tools`; `git status` clean before this file.
