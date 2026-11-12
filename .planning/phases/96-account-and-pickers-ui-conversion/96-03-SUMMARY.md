---
phase: 96-account-and-pickers-ui-conversion
plan: 03
subsystem: gate1-package
tags: [mockups, picker, home-merge, overflow, scratch-tooling, gate-1]
requires:
  - phase: 96-02
    provides: fixtures, manifest, probes, frozen baseline on 8001, BEFORE capture
provides:
  - picker flat-row mockup with 68 frames, a shippable-shape group-picker.css and a keyboard pass
  - unified Home landing for both layouts with a merge proposal page
  - measured AccessDenied/Events-Details overflow placement (64 records)
  - Gate 1 sheet sections A (picker), C (Home) and D (overflow); Profile section left for 96-04
affects: [96-04, 96-05, 96-06 and every later slice that runs a contrast sweep]
tech-stack:
  added: []
  patterns: [mockup frames driven by real mouse and Tab presses, per-state contrast measured on the row in that state, stack-aware panel contrast, scratch-folder copy of the foundation sheets for file:// mockups]
key-files:
  created: []
  modified: []
key-decisions:
  - "No repository file changes: every artifact lives under ~/qb-ui-review/phase-96, so no task commit exists; the only commit is the docs commit for this summary and the state files"
  - "Mockups link a copy of the frozen baseline's foundation sheets (mockups/v1/foundation) instead of http://localhost:8001/css/, because a file:// page cannot load fonts from the app (CORS: no Access-Control-Allow-Origin)"
  - "Contrast probe gains an opt-in stack mode and understands color-mix() computed values; the default sweep behaviour is unchanged"
  - "A sixth picker class group-picker-text (the stacked name and count block) is added to the five the plan lists"
  - "A fourth Gate 1 picker question was added: the phone title step"
requirements-completed: [D-01, D-02, D-07, D-08, DoC-6]
duration: ~2.5 h
completed: 2026-11-12
status: complete
actuals:
  tokens: 90000
  tasks: 3
  commits: 1
---

# Phase 96 Plan 03: Gate 1 package, first half Summary

The picker flat-row mockup, the unified Home look with its merge proposal and the measured overflow placement are drawn, probed and on the Gate 1 sheet, and no repository file changed. The picker mockup's 68 frames (every case, state, chevron and shape comparison, at 1440 desktop, 390 phone User-Agent and 320 desktop) report no contrast failure, no overflow, no text-spacing overflow, no missing focus ring and no phone target under 44px.

All paths are under `~/qb-ui-review/phase-96` (`QBR`). Open `QBR/gate1/index.html`.

## Task 1 (tracer): one picker frame end to end (verify printed `ok` twice)

`mockups/v1/picker/group-picker.css` is the whole replacement sheet: a plain-language header comment and one `@layer pages { }` block (list reset, `--qb-border` separator between rows, the UA button reset exactly as UI-SPEC section 1 fixes it, name, meta and chevron, hover `color-mix(in srgb, var(--qb-surface-scrim) 60%, transparent)`, pressed `var(--qb-surface-scrim)`, `:focus-visible` `position: relative; z-index: 1`, a `background-color` transition only, and the phone `@media (max-width: 767.98px)` block). No colour literal, no important flag, no `transition: all`, no ring code. `index.html` reproduces the picker layout's shell and links the foundation sheets in the layout's order, then this sheet. It has no inline style, style block or inline script (the switcher lives in `picker.js`). The first two frames (1440 and 390) were captured and placed beside their BEFORE on `gate1/index.html` section `picker`.

## Task 2: every case and state, the alternatives, a keyboard pass (verify printed `ok` x3)

`frames.json` lists 68 frames; `png/report.json` has 68 records. Frame groups: 8 cases (one, three, twelve, long-name, long-token, no-boards, super-with-boards, super-no-boards) at 1440, 390 and 320 = 24; states (hover, pressed, focus first row, focus middle row) on the three-board case at each viewport = 12; wrapped-name hover and focus and the long-token focus at each viewport = 9; chevron off (three and long-name, three viewports, plus one with focus) = 7; lifted-card alternative (three and long-name at three viewports, hover, pressed and focus at 1440 and 390, twelve at 1440 and 390) = 14. Plus two phone-title frames. Hover and press are driven with the real mouse and focus with real Tab key presses.

Picker probe numbers (minimum over the blocking boards, glass stacked on scrim, row in the stated state):

| Pair | State | UI-SPEC | Measured |
|------|-------|---------|----------|
| name `--qb-fg` | rest / hover / pressed | 6.70 / 8.47 / 9.88 | 6.70 / 8.48 / 9.88 |
| member count `--qb-fg-muted` | rest / hover / pressed | 6.09 / 7.69 / 8.97 | 6.09 / 7.71 / 8.97 |
| chevron `--qb-fg-muted` | rest | 6.09 | 6.09 |
| nested-glass hover fill (not offered) | hover | 4.47 / 4.06 | 4.47 / 4.06 (fails, two frames in `png-reference/`) |
| lifted card hover | hover | n/a | 6.20 / 5.63 (passes: a card carries its own scrim) |

Geometry: flat rows are 61px (109px for the wrapped 60-character name at 390 and 320), the chevron keeps its 9px width in every frame, and the long-name and long-token frames at 390 and 320 report `overflow: false`. The twelve-board panel is 860px tall flat against 1241px lifted at 1440; a lifted card is 77px against 61px.

Keyboard pass (`tools/mockup-keyboard.mjs`, results for all eight cases in `gate1/picker-keyboard.json`, every case `ok` under both User-Agents): Tab order is skip link, Logout, Go to Platform when present, then every `.group-picker-row` in list order and nothing else; the ring extent is 4px and lies inside the panel's padding box with no clipping ancestor; Enter and Space each submit that row's own form once (submit intercepted, `groupId` recorded). A mutation check (an `overflow: hidden` card body) makes it fail, so it can fail.

Four questions are on the sheet with a recommendation each: row shape (flat), chevron (on), hover tint (60% scrim) and the phone title step (see Deviations 4).

## Task 3: Home look, merge proposal, overflow placement (verify printed `ok` twice)

Home (`mockups/v1/home/`, `landing.css` with one `@layer pages` block, `shell-rule.css`, a `<template>`-based `index.html` plus `home.js`): one centred glass card, dice icon in the title colour, `h1`, the sentence at body contrast, `<hr>`, `Log in` as `btn-primary btn-lg` inside `.landing-action`. Probes: 1440 and 390 report no contrast failure and no overflow; sentence 6.70, title 6.70, button label 7.10; at 390 the button is 324px wide against a 324px content width and 48px tall. `gate1/home-merge.html` carries the BEFORE pair, the AFTER, the three merge criteria (all met), what a merge changes, the tests it touches (`HomeControllerIntegrationTests`: `Log In` at two sites, changes only if the answer is `Log in`; `MobileCssTests`, `MobileLayoutTests`: constraints, unchanged; `MobileViewLocationExpanderTests`: unaffected), what happens on a no, reversibility (costly) and exactly two questions: merge yes or no, and `Log in` or `Log In`.

Overflow (`tools/overflow-compare.mjs`, `gate1/overflow.json`, 64 records), `scrollWidth` against the viewport on the frozen baseline:

| Page | Viewport | As-is | Shell rule | Fallback (AccessDenied only) | Stressed (as-is / rule / fallback) |
|------|----------|-------|------------|------------------------------|------------------------------------|
| accessdenied-in, accessdenied-out, fx-row-shared-accessdenied | 390 phone | 394 | 390 | 390 | 394 / 390 / 390 |
| same | 412 phone | 416 | 412 | 412 | 416 / 412 / 412 |
| accessdenied-in, accessdenied-out | 320 desktop | 320 | 320 | 320 | 320 / 320 / 320 |
| fx-events-details, fx-row-events-details | 390 phone | 394 | 390 | not applicable | 394 / 390 / n/a |
| same | 412 phone | 416 | 412 | not applicable | 416 / 412 / n/a |

The card-edge shift the shell rule causes on every fx-row page is 0px on both edges (cards stay at 8px left, 382px right at 390): the row margin and the column padding shrink by the same 4px, so only the invisible overshoot goes away. The unified Home card has the same 394/416 overflow without the rule (frames in `png-reference/home-phone-norule--m390.png` and `--m412.png`), which the sheet and merge page state. Sections C (`id="home"`) and D (`id="overflow"`) are on the sheet; the recommendation is the shell rule, because Events/Details (four cards under one root-level row, a real column grid inside) is helped by it and not by the fallback, and Home needs the same fix.

## Deviations from Plan

### Operator-approved environment substitutions (not defects)

**1. [Environment] Windows plan assumptions replaced with Linux equivalents** (per `ENVIRONMENT.md`): baseline on port 8001 where the plan says 8097, `python3`, bundled Chromium, `qb-audit-*` accounts. Files: scratch only.

### Auto-fixed Issues

**2. [Rule 3 - Blocking] Mockups cannot link `http://localhost:8001/css/`.** A page opened from disk requests the app's fonts with an opaque origin and the app sends no `Access-Control-Allow-Origin`, so Cinzel and Font Awesome do not load (verified: both fonts `error`, CORS message). The plan's key link therefore changed: the mockups link `mockups/v1/foundation/` (css, bootstrap, Font Awesome, Cinzel and the two board photos copied from `baseline-src`, 1.5 MB), the same files the frozen baseline serves, as the phase 95 mockups did. Files: scratch only.

**3. [Rule 1 - Bug] The contrast probe scored the glass panel wrongly.** `probes.mjs` scored an element with two background layers (the `.modern-card` glass over scrim) as one alternative surface per colour stop, i.e. glass alone, so every 14px `--qb-fg-muted` pair on the panel measured 4.25 (name 4.68) against the UI-SPEC's 6.09 (6.70), which is the real stack. Fix: an opt-in `stack` mode (`runProbes(page, { stack: true })`) that scores the layers as the stack they are; the default is unchanged, so BEFORE and the existing sweeps behave as before. Second bug in the same path: `contrast.mjs` did not parse `color(srgb ...)`, the computed form of `color-mix()`, so the hover and pressed tint was silently dropped from every score; `parse` now reads it (a hovered row measures 8.48 against 6.70 at rest, matching the UI-SPEC). Files: `tools/probes.mjs`, `tools/contrast.mjs` (scratch). **Consequence for later plans:** an AFTER sweep run with the default probe will report the 14px muted text on any panel as 4.25 and trip `--zero-contrast`. Run the sweep with the stack mode on (one line in `sweep.mjs`) or accept the exception; BEFORE was captured without it, so its panel-text failures are partly this artefact.

**4. [Rule 2 - Missing critical functionality] Phone title step.** The picker has no phone twin, so the foundation's 20px phone title rule (`.mobile-layout ...`) never applies and the new `h1` wraps to two lines in Cinzel 28px at 390 and 320. The mockup's phone `@media` block carries `.group-picker .modern-card-header h1 { font-size: var(--qb-text-lg) }`; the sheet shows both and asks. Not in the UI-SPEC.

**5. [Plan wording] `group-picker-text` added** as the class of the stacked name and count block (the UI-SPEC describes it as "the text block" without a class). Six classes are ported, not five.

**6. [Plan wording] Frame names.** The Task 2 verify needs a frame name containing `lifted-card`; the lifted frames are named `*-lifted-card-*`.

**7. [Plan wording] Reference frames kept out of `png/report.json`.** The nested-glass hover frames (which must fail) and the Home frames without the shell rule (which must overflow) live in `png-reference/`, and the Home frames in `png-home/`, so the picker report stays an all-clear record.

## Auth gates

None.

## Known Stubs

None. No repository files were created or modified by this plan.

## Threat Flags

None. `git status --porcelain` on the repository is empty after this plan (HEAD e851a58b plus this docs commit); mockups use fantasy names only (the 60-character board name is the fixture name); the overflow probe only injects style into headless pages and the frozen baseline was not written to.

## Open Gate 1 questions placed on the sheet

Picker (A): row shape, chevron, hover tint, phone title step. Home (C): merge yes or no, `Log in` or `Log In`. Overflow (D): shell rule or AccessDenied-local fallback. The Profile section, the full item list and the gate itself are 96-04.

## Self-Check: PASSED

Verified: Task 1 verify printed `ok` x2; Task 2 verify (both keyboard runs, frames.json names, report) printed `ok`; Task 3 verify printed `ok` x2 (as-is AccessDenied at 390 on the phone reproduces 394, shell-rule variant fits everywhere, Events/Details measured); `gate1/index.html`, `gate1/home-merge.html`, `gate1/overflow.json`, `mockups/v1/picker/{index.html,group-picker.css,frames.json,picker.js}`, `mockups/v1/home/{index.html,landing.css}`, `mockups/v1/png/report.json` (68 records) exist; every image the sheet references exists; the repository has no changes.
