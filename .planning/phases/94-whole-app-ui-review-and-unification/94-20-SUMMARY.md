---
phase: 94-whole-app-ui-review-and-unification
plan: 20
subsystem: ui-foundation
tags: [components-layer, cards, buttons, forms, alerts, tables, badges, modal, toasts, aria, wcag-contrast, guard-tests]
requires:
  - phase: 94-19
    provides: "Bootstrap 5.3.8 in layer(vendor), --bs-* mapping in base.css, focus ring, reduced motion"
  - phase: 94-18
    provides: "token layer, components.css scaffold, WcagContrast helper and the token-contrast guard"
provides:
  - "components.css: card family, page header, buttons by role, forms and validation, alerts, tables, badges, modal and offcanvas, toast surface and stack, empty state, refusal line, utilities layer tail"
  - "tokens.css: hovered glass tint eased so status text reaches 4.5:1 (contrast ledger entry 10 resolved); the guard now scores hovered glass against the full text list"
  - "_Toasts.cshtml rebuilt: error and warning persist, status roles, named close buttons, no inline style"
  - "UiFoundationComponentGuardTests (9 facts) and UiFoundationToastGuardTests (5 facts)"
affects: [94-21, 94-22, 94-23, 94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "Components set Bootstrap's own variables per role; no important flag and no colour literal in components.css"
    - "Rule-source guard tests parse the stylesheet into rules with their enclosing at-rules, with a parser self-test"
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/UiFoundationComponentGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationToastGuardTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/wwwroot/css/tokens.css
    - QuestBoard.Service/wwwroot/css/modern-card.css
    - QuestBoard.Service/Views/Shared/_Toasts.cshtml
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationBaseGuardTests.cs
decisions:
  - "Hovered glass eased from 0.18 to 0.15 white instead of lightening the signed-off status text colours"
  - "Alerts are an opaque parchment panel with ink text and a status stripe, not the dark overlay panel of the mockup, because the legacy global heading rule paints headings dark and would be unreadable on a dark panel until it is retired"
  - "A status colour used as text means ink on paper surfaces (tables, alerts, rows): the fills measure 4.24 to 4.32 on parchment and amber 1.68"
  - "The mobile 20px page-title step on .modern-card-header is not applied yet (see Findings 2)"
  - "Gold toast is announced politely (role=status) like the other confirmations"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 5
---

# Phase 94 Plan 20: Component Layer and Toasts Summary

The shared component layer is in `@layer components` (cards, buttons by role, forms, alerts, tables, badges, modal and offcanvas, toasts, empty state, refusal line, plus a `utilities` layer tail), the hovered-glass shortfall for status text is closed at the token, and the toast partial persists errors and warnings with proper roles and named close buttons.

## Commits

| Task | Commit | What |
|------|--------|------|
| 2 (WINDOWS 10) | `8eacccf6` | hovered glass 0.18 to 0.15, guard exclusion removed |
| 1 (tracer) | `ceb8552d` | card family, `UiFoundationComponentGuardTests` (2 facts + parser self-test) |
| 2 | `4dafd638` | the rest of the component set, six guard facts, paper-status and alert decisions |
| 3 | `e36f9b99` | `_Toasts.cshtml`, toast rules out of `modern-card.css`, `UiFoundationToastGuardTests` |

## WINDOWS entry 10 resolved: status text on hovered glass

The hover tint was eased, the signed-off status colours are untouched. Lightest board pixel (`#794317`) under the 42% scrim, text on hovered glass:

| Text token | Before (0.18) | After (0.15) |
|---|---|---|
| success `#7fdca4` | 4.23 | 4.64 |
| danger `#ffbab1` | 4.31 | 4.73 |
| warning `#ffbf5e` | 4.30 | 4.71 |
| info `#8fd3ee` | 4.25 | 4.66 |
| event `#d9c8ff` | 4.56 | 5.00 |
| neutral `#d0d3d6` | 4.67 | 5.12 |
| parchment / muted / link / link-hover / accent text | 5.57 / 5.06 / 5.44 / 6.32 / 5.14 | 6.11 / 5.55 / 5.97 / 6.93 / 5.64 |

Rest glass (0.12) is unchanged (5.10 to 5.19 for the status texts). The Shop theme hover (0.12 on its own scrim) already passed (6.0 and up). The `StatusTextOnHoverShortfall` list and its comment are gone from `UiFoundationLayerGuardTests`; "Text on hovered glass" takes the full text list again. Negative check: putting the hover back to 0.18 makes `TokenContrast_DeclaredPairsMeetTheBar` fail with `--qb-success-text on hovered glass is 4.23, needs 4.5`. The tokens header's alpha list names 0.15 in place of 0.18. No colour literal added; the opaque-hex count is unchanged at 38. The entry can be marked fixed (commit `8eacccf6`).

## What was built

- **Card (Task 1).** Glass over the 42% scrim as two background layers, `color: var(--qb-fg)` set once on the surface, no backdrop blur on the repeated item, rest shadow token. Header, body and footer; the title in `.modern-card-header` takes the display face at 28px with 0.04em letter spacing (only `h1` and `h2` there, so buttons and badges keep the body face); `.header-subtitle` at the label step in the muted token. Hover lift only on `.modern-card--interactive`, transform and shadow inside `prefers-reduced-motion: no-preference`; the lighter tint on that card is outside the query.
- **Buttons.** `--bs-btn-*` per role from the action, status and `-fg` tokens; hover and pressed darken (warning lightens on hover so its near-black label only gains). Label contrast, rest / hover / pressed: primary 7.10 / 5.62 / 8.88, secondary 6.09 / 7.64 / 8.91, success 5.35 / 6.90 / 8.21, danger 5.44 / 6.97 / 8.26, info 5.67 / 7.25 / 8.57, warning (near-black) 8.92 / 9.84 / 6.71. One disabled look for every role (neutral fill, white label 6.09, opacity fade off) so a form locked after submit stays legible; `site.js` is untouched. `.btn-dark` and `.btn-light` exist for the toast and paper cases.
- **Forms.** Parchment control with ink text, border at 3:1 on its own fill, 16px, placeholder in the muted ink (9.67 on the control fill), disabled and readonly on the alternate paper, invalid border 2px in the danger fill (about 5.3 on the field), validation text (`.invalid-feedback`, `.field-validation-error`, `.validation-summary-errors`) in the danger text variant, checked boxes wood with white mark (7.10) edged in parchment.
- **Alerts, tables, badges.** Alerts: parchment panel, ink text, 4px status stripe. Tables: parchment cells with ink, wood header with parchment (5.63), striped and hover rows stay dark. Badges: `--bs-badge-font-size` 14px, weight 600, radius small, white label, near-black on amber.
- **Overlays.** `.modal`, `.offcanvas` and `.toast` read the opaque overlay surface and parchment text (12.98); the close button is drawn light on them. On the phone layout a dialog is capped to the viewport, the body may scroll, and buttons are at least 44px.
- **Empty state and refusal line.** `.empty-state`, `.empty-state-icon`, `.empty-state-heading`, `.empty-state-text` (structure only), `.refusal-line` (flex, icon and sentence, danger text variant).
- **Utilities.** `@layer utilities` holds `.visually-hidden-focusable` (hidden unless focused), `.text-halo` and `.text-purple`.
- **Toasts.** Error and Warning: `role="alert" aria-live="assertive" aria-atomic="true" data-bs-autohide="false"`. Success, Info and Gold: `role="status" aria-live="polite" aria-atomic="true"`, delays 5000, 5000 and 6000. Board-switch and moved keep `class="toast show board-switch-toast" role="alert" data-bs-autohide="false"` in the pinned order, the moved header still starts `toast-header bg-success`, and `Url.IsLocalUrl(movedEntryUrl)` is kept. Container: inline style and `top-0` replaced by `qb-toast-stack` (`top: var(--qb-nav-h)`, capped at 24rem). Every `btn-close` has `aria-label="Close"`; `text-break` on every body. The "Switch back" comment now states its reason itself (filled dark button, not outline; label about 14.9:1 on the button fill and the pale edge about 4.6:1 against the toast body, measured). No toast sentence changed. From `modern-card.css` only the board-switch background rule and the 767.98px offset were deleted, with their comments.

## Verification

- **Gallery (scratch `plans/94-20/gallery.html`).** Every component, crowded and empty, under the desktop and mobile User-Agent with `--probes`: 0 contrast failures, 0 console errors, 0 failed requests, no overflow (the table scrolls inside `.table-responsive`). The shared probe flattens multi-layer glass (ledger entry 8), so the same page was also scored with the corrected package probe `probe-lib.mjs`: 122 elements tested per User-Agent, 0 failures. The gallery has to be served by the app for the fonts (a `file://` page is blocked by CORS), so `shoot-gallery.sh` copies it into the worktree's `wwwroot/_gallery/` for the capture and the copy was removed afterwards; nothing of it is committed. The plan's literal `file://` verify command therefore was not used.
- **Readability comparison.** Sweeps on port 8020: `p20-before` (the components and tokens files of the base commit swapped in, same app), `p20-after` (after Task 2) and `p20-final` (all tasks), 120 records each, 0 non-200. The shared `readability-compare.py` prints FAIL on 7 rows, all of the same kind: badges and form labels that were already failing now measure at 14px instead of 12 or 16px, and the shared tool keys a failure without a text sample by size, so the same failure reads as new (same family as ledger entry 14, now for font size). The corrected comparison `plans/94-20/compare3.py` (keyed by selector, sample and bar, with counts) reports **0 new, 0 worse, 10 improved or gone**, and no row has a hidden control, console error, failed request or overflow. The first Task 2 pass did flag 6 real new failures (five mobile page titles at 20px, and a dark heading inside a dark alert panel); both causes were removed (Deviations 1 and 2).
- **WINDOWS 12.** Where the component rules apply, these moved: desktop `ShopManagement-Edit` `small.text-muted` 2.45 to 3.05, `Platform-LinkedBoards-Index` success button 2.01 to 2.87, `Platform-Users-Index` small button 2.41 to 2.87, mobile `Agenda-Index` and `Events-Index` header subtitle 1.11 to 4.0, mobile `Characters-Details` Edit button 1.29 to 1.68, and three failures disappeared (mobile `Admin-Users` amber badge 2.11, `Platform-LinkedBoards-Index` success button 2.83, `Platform-Users-Index` small button 2.68, `ShopManagement-Edit` muted text 1.19). The ten light-status-text failures are not resolved here, because the legacy glass still has no scrim; they remain for the card retirement.
- **Toasts (`plans/94-20/toasts.mjs`, port 8020, player).** Error flow used: `GET /Account/ConfirmEmailChange?userId=0&newEmail=nobody@example.test&token=abc` as the Audit Player. There is no user 0, so nothing is written; the controller sets `TempData["Error"]` and redirects to the login page, which renders the partial. Both User-Agents: toast visible at once, opaque overlay brown with parchment text, **still visible after 8s**, closes by click on Close, closes by keyboard (Tab to Close, Enter); a stack of the real error toast plus injected copies of the Success, Info and board-switch markup sits below the navbar (bar bottom 64px, container top 56px, first toast top 72px), stays inside the viewport and covers nothing; after 6.5s both timed toasts are gone and both persistent toasts remain; closing one leaves the other. The injected copies are needed because the partial renders only what TempData holds in one request; the Error, persistence and keyboard checks run on the real rendered partial. 390px under the mobile User-Agent: the Shop modal (opened from the item card) and the character-select modal (opened programmatically on the finalized fixture quest, which renders it) fit the width (8 to 382), the body or dialog scrolls with the last button reachable, and every button and close button is at least 44px. Final line: "ALL CHECKS PASSED". Screenshots: `toast-stack-{desktop,mobile}.png`, `modal-{shop,character}-390.png`.
- **Guards and suite.** `UiFoundation*` 65 facts green. `BoardMoveControlTests` and `CrossBoardPickerSkipTests` pass unchanged. `dotnet build` 0 errors; full `dotnet test`: 1300 unit and 1726 integration tests pass (11 skipped, pre-existing). One `dotnet build` hit the known CLR flake and passed on the single rerun. Local negative checks (not committed): a `.modern-card:hover` transform and a `.modern-card p` colour make the two card facts fail; the old hover alpha makes the contrast guard fail.
- Port 8020 stopped; 8000 and 8001 untouched. No migration, no data written. The worktree has no untracked files.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Mobile 20px page-title step caused 5 new readability failures**
- **Found during:** Task 2 sweep comparison
- **Issue:** `.mobile-layout .modern-card-header :is(h1, h2)` at 20px turns the page title into normal text (4.5:1), and on today's legacy glass (no scrim yet) it measures 3.91. Same class of problem as the reverted heading scale.
- **Fix:** removed the mobile size rule; the mobile title keeps the vendor size until the card surfaces carry the scrim. The desktop 28px title is large text and does not regress.
- **Files modified:** `components.css`. **Commit:** `4dafd638`

**2. [Rule 1 - Bug] Dark alert panel made a legacy dark heading unreadable**
- **Found during:** Task 2 sweep comparison (`ShopManagement-Edit` mobile, `h6.alert-heading` 1.15)
- **Issue:** the legacy page sheets paint every heading dark with an unlayered rule that a layered rule cannot override, so the mockup's dark alert panel hid headings inside alerts.
- **Fix:** alerts are a parchment panel with ink text and a status stripe, mapped like the other paper surfaces. The dark overlay look can come back when the legacy global heading rule is retired.
- **Files modified:** `components.css`. **Commit:** `4dafd638`

**3. [Rule 2 - Missing critical] Status colours as text on paper failed the bar**
- **Found during:** Task 2 gallery probe (4 failures)
- **Issue:** the earlier paper mapping gave success, danger, warning and info text their fills: 4.24, 4.32, 1.68, 4.499 on parchment.
- **Fix:** on tables, alerts and paper rows a status colour used as text means ink (the word and icon carry the status). `UiFoundationBaseGuardTests.ComponentsCss_MapsBootstrapVariablesPerSurface` was updated to the new mapping (`--bs-danger-rgb: var(--qb-ink-rgb)`); that file is not on the plan's list.
- **Files modified:** `components.css`, `UiFoundationBaseGuardTests.cs`. **Commit:** `4dafd638`

**4. [Rule 1 - Bug] The toast was transparent on every desktop layout**
- **Found during:** Task 3 screenshot (parchment text on a white page)
- **Issue:** `site.css` has a bare unlayered `div { background: transparent; }`. It outranks any layered background on a `div`, including Bootstrap's own toast surface since the vendor layering, so the toast showed what was behind it; with the new parchment toast text it was unreadable.
- **Fix:** one unlayered rule in `modern-card.css` (loaded wherever `site.css` is) restates `background-color: var(--bs-toast-bg)`, with a plain-language comment. The plan said to delete only two rules from that file; this is an addition, removed with the file.
- **Files modified:** `modern-card.css`. **Commit:** `e36f9b99`

**5. [Rule 3] Bootstrap 5.3.8 has no `--bs-btn-close-white-filter`**
- The modal and offcanvas close button is lightened through `--bs-btn-close-filter`, the variable this release reads.

**6. [Scope] Tracer gate.** Neither `workflow.auto_advance` nor the chain flag is set, but the plan is `autonomous: true` and the orchestrator directed a full run, so the interactive human-verify stop was replaced by re-running the tracer's verify end to end (guard tests, gallery probes, both User-Agents). It passed before Task 2 started.

**7. [Scope] Readability baseline.** `p20-before` is the base commit's `components.css` and `tokens.css` swapped into the running worktree app (CSS is served from disk), so it differs from `p20-after` only by this plan's edits, including the eased hover tint.

**8. [Scope] Files beyond the plan's list.** `UiFoundationLayerGuardTests.cs` (the guard exclusion is there, not in `UiFoundationWcagContrastTests`) and `UiFoundationBaseGuardTests.cs`.

## Findings for the orchestrator

1. **`site.css` `div { background: transparent; }` defeats every layered surface on a div (desktop layouts only).** Since the vendor layering, Bootstrap's own `.alert`, `.modal-content`, `.card`, `.toast`, `.dropdown-menu` and similar div surfaces, and any component surface this plan adds on a div, lose their background to that unlayered type rule (specificity 0,0,1 still beats a layer). The phone layout does not load `site.css` and is unaffected. I worked around it for the toast only. The rule should be retired with the other legacy globals in the card-retirement plan (check `Quest Log`, which has its own exception right below it), and the toast workaround removed with `modern-card.css`. Suggested ledger entry: kind `deviation`, file `QuestBoard.Service/wwwroot/css/site.css`, "bare div background rule defeats layered div surfaces on desktop; toast restated in modern-card.css".
2. **Re-land the mobile page title step with the card retirement.** Add `.mobile-layout .modern-card-header :is(h1, h2) { font-size: var(--qb-text-lg); }` to `components.css` in the same plan that re-lands the heading scale (it is the Display step on the phone layout and needs the scrim to pass).
3. **Dark alert panel (the signed-off mockup look)** can replace the parchment panel once the legacy global heading rule is gone (Deviation 2). Until then alerts on desktop are also transparent because of Finding 1.
4. **Shared comparison tool.** `readability-compare.py` keys a no-sample failure by font size, so a component that changes size (badge 12 to 14px, form label 16 to 14px) reads as new. Same family as entry 14; `plans/94-20/compare3.py` is a size-free key.
5. **`.modern-card` and the other layered card rules do not show on live pages yet** (legacy `modern-card.css` is important and unlayered), as expected; the gallery is the proof for those.

## Known Stubs

None. `.empty-state` and the `.refusal-line` rule are deliberate structures with no page using them yet, as planned.

## Threat Flags

None new. T-94-35: Razor auto-encoding kept, `Html.Raw` absent (guarded in `MovedToast_KeepsLocalUrlCheck`). T-94-36: `Url.IsLocalUrl(movedEntryUrl)` kept and guarded. T-94-37: error and warning toasts persist until dismissed (guarded, and shown live at 8 seconds on both User-Agents).

## Self-Check: PASSED

- Files present: `components.css`, `tokens.css`, `modern-card.css`, `_Toasts.cshtml`, `UiFoundationComponentGuardTests.cs`, `UiFoundationToastGuardTests.cs`.
- Commits present: `8eacccf6`, `ceb8552d`, `4dafd638`, `e36f9b99`.
- STATE.md, ROADMAP.md and WINDOWS.md not modified. Scratch under `~/qb-ui-review/phase-94/plans/94-20/` (gallery, `toasts.mjs`, `compare3.py`, captures, logs); sweeps `p20-before`, `p20-after`, `p20-final`.
