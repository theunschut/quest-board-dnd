---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 07
subsystem: ui
tags: [easymde, codemirror, css-specificity, markdown, human-verification]

# Dependency graph
requires:
  - phase: 66-01
    provides: Html.Markdown() / ExtractPlainText read primitives
  - phase: 66-02
    provides: POST /markdown/preview round-trip endpoint
  - phase: 66-03
    provides: Quest Finalized email Markdown rendering
  - phase: 66-04
    provides: shared EasyMDE editor client assets
  - phase: 66-05
    provides: editor wired into Create/Edit/Follow-Up write forms
  - phase: 66-06
    provides: Markdown rendering on Details/Manage/board card
provides:
  - Operator sign-off on the full write->read->email Markdown loop
  - Fix for a CSS specificity bug that tinted the raw editor/Preview pane gold instead of black
  - Heading-level size hierarchy (1.5rem->0.875rem) added to .markdown-content, matching the
    editor's own cm-header-N differentiation
affects: [67, 68, 69, 70]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "CSS specificity bump via doubled class selector (.CodeMirror.CodeMirror) to robustly beat
       an app-wide !important rule regardless of stylesheet load order"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/markdown-editor.css
    - QuestBoard.Service/wwwroot/css/markdown-content.css

key-decisions:
  - "44px touch-target width (vs. height) deviation accepted as-is: mobile Description editing
     is a minor use case, buttons are 44px tall / ~30-34px wide, and no overflow occurs -- operator
     explicitly declined to route this to gap closure."
  - "Editor/Preview text color and shadow forced to plain black via a specificity-safe override,
     since .modern-card's gold styling (meant for read-only card text) was leaking into
     EasyMDE's CodeMirror syntax-highlighting spans and the Preview pane's injected HTML."
  - "Heading levels in rendered Markdown (.markdown-content) now step from 1.5rem (h1) to
     0.875rem (h6) instead of one flat 1.25rem tier, per operator request, so Details/Manage
     visually match the hierarchy already visible while editing."

patterns-established:
  - "Pattern: when overriding a third-party library's CSS to beat an app-wide !important rule,
     bump specificity via a doubled class selector rather than relying on stylesheet load order."

requirements-completed: [EDITOR-01, EDITOR-02, EDITOR-03, EDITOR-04, EDITOR-05, EDITOR-06, QUESTMD-01]

# Metrics
duration: ~110min
completed: 2026-07-09
status: complete
---

# Phase 66: Quest Description Editor & Rendering (Proof of Concept) Summary

**Operator-verified write->read->email Markdown loop for Quest Description, with a CSS specificity fix for the editor/Preview text color and a new heading-hierarchy scale added to rendered Markdown output.**

## Performance

- **Duration:** ~110 min (includes live browser verification and iterative CSS fixes)
- **Started:** 2026-07-09T15:04:08Z
- **Completed:** 2026-07-09T21:27:29Z
- **Tasks:** 2/2 checkpoint tasks confirmed by operator
- **Files modified:** 2

## Accomplishments
- Operator confirmed the full write (EasyMDE editor + Preview) -> read (Details/Manage/board card) -> email (Quest Finalized) Markdown loop works end-to-end in a real browser, at both desktop and mobile widths.
- Found and fixed a CSS specificity bug where `.modern-card`'s `color`/`text-shadow` rule (intended for read-only card text) leaked into the raw CodeMirror editing surface and the Preview pane via matching `<span>`/`<p>`/`<li>` elements, making editor text unreadable gold-on-white instead of black-on-white.
- Added heading-level size differentiation to `.markdown-content` (Details/Manage/board card rendering) so `#`/`##`/`###` etc. are visually distinguishable there, matching the differentiation EasyMDE's own `cm-header-N` classes already show in the editor -- previously all six heading levels collapsed to one flat 1.25rem size.

## Task Commits

Both checkpoint tasks were verification-only (no code changes required by the plan itself); the operator's follow-up findings during verification were fixed and committed atomically:

1. **CSS specificity + heading hierarchy fix** - `9b80bade` (fix)

_Note: this plan's own two `checkpoint:human-verify` tasks produced no commits themselves -- they gated on operator confirmation. The commit above addresses issues the operator raised while performing that verification._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/markdown-editor.css` - Added a specificity-safe override (`.CodeMirror.CodeMirror`, `.editor-preview.editor-preview`) forcing black text / no shadow on the editing and Preview surfaces, beating `.modern-card`'s leaking gold rule
- `QuestBoard.Service/wwwroot/css/markdown-content.css` - Replaced the flat `1.25rem` heading rule with a stepped `1.5rem` (h1) -> `0.875rem` (h6) scale

## Decisions Made
- **44px touch-target width left as-is.** Toolbar buttons at 320px are 44px tall but only ~30-34px wide due to an EasyMDE-vs-app CSS cascade tie on `min-width` (EasyMDE's own CDN stylesheet loads after the app's override and wins on source order for that specific property). No overflow occurs. Operator judged mobile Description editing to be low-usage and explicitly accepted the deviation rather than routing to gap closure.
- **Editor/Preview color bug: fixed, not deferred.** Unlike the touch-target case, this was judged a genuine, easily-fixable bug (not a competing design constraint) and fixed in-session.
- **Heading hierarchy: added by operator request**, going beyond the original plan's must-haves (which only required the flat-collapse behavior already in place from 66-04/66-06). Scoped entirely under `.markdown-content` per explicit operator instruction ("only apply the override to the markdown renderers") -- verified the page's own `<h1>`/`<h2>` elements outside `.markdown-content` (e.g. "Manage Quest: test") are unaffected.

## Deviations from Plan

### Auto-fixed Issues

**1. [Operator-directed fix] CSS specificity bug tinting editor/Preview text gold instead of black**
- **Found during:** Task 1 human verification (operator screenshot review)
- **Issue:** `.modern-card span/p/li/small { color: #F4E4BC !important; text-shadow: ... !important; }` (site.css) matched CodeMirror's internal syntax-highlighting spans and the Preview pane's injected HTML, since both live inside a `.modern-card-body` wrapper on the desktop Create/Edit views.
- **Fix:** Added `.CodeMirror.CodeMirror`, `.CodeMirror.CodeMirror *`, `.editor-preview.editor-preview`, `.editor-preview.editor-preview *` rules in `markdown-editor.css` forcing `color: #000 !important; text-shadow: none !important;`. The doubled class beats the leaking rule on specificity (2 classes vs. 1 class + 1 element) regardless of stylesheet load order -- a first attempt using `.CodeMirror *` failed because the universal selector contributes zero specificity.
- **Files modified:** `QuestBoard.Service/wwwroot/css/markdown-editor.css`
- **Verification:** Live browser check confirmed black text with no shadow in both the raw editor and Preview pane; `.markdown-content` on Details/Manage retained its original gold styling.
- **Committed in:** `9b80bade`

**2. [Operator-requested enhancement] Heading levels now visually differentiated in rendered Markdown**
- **Found during:** Task 1/2 verification follow-up -- operator asked why `#`/`##`/`###` all rendered the same size on Details/Manage despite differing sizes in the editor.
- **Issue:** `.markdown-content h1..h6` collapsed every heading level to one flat `1.25rem`, which was the original 66-04/66-06 design intent (avoid a full-document-sized H1 inside a compact card) but did not match the editor's own `cm-header-N` differentiation, and the operator wanted that hierarchy visible on the rendered side too.
- **Fix:** Replaced the flat rule with a stepped scale (`h1: 1.5rem` down to `h6: 0.875rem`), capped below any full document-heading size, scoped entirely under `.markdown-content` per explicit operator instruction not to change app-wide heading styling.
- **Files modified:** `QuestBoard.Service/wwwroot/css/markdown-content.css`
- **Verification:** Live browser check on Details and Manage confirmed the new stepped sizes (24/22/20/18/16/14px) and confirmed the page's own non-`.markdown-content` headings were unaffected.
- **Committed in:** `9b80bade`

---

**Total deviations:** 2 auto-fixed (both operator-directed during human verification, not autonomous Rule 1-5 fixes)
**Impact on plan:** Both fixes are scoped, CSS-only changes discovered during the plan's own verification process; no scope creep beyond what verification surfaced.

## Issues Encountered
- Mobile view detection in this app is server-side User-Agent sniffing (`MobileDetectionMiddleware`), which the preview tooling doesn't spoof -- automated 320px-viewport checks earlier in this plan's execution used the desktop `Create.cshtml` resized to 320px rather than the true `Create.Mobile.cshtml`. The CSS bug fixed in this plan lives in a shared stylesheet loaded by both layouts, so it very likely applied identically, but this was not independently confirmed against the real mobile-detected page.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The full write->read->email Markdown loop for Quest Description is proven end-to-end and operator-approved.
- The doubled-class specificity-bump pattern established here (`markdown-editor.css`) is directly reusable for phases 67-70, which wire the same shared editor onto additional fields -- any field editor nested inside a `.modern-card` will need the same override if it hits the same `.modern-card span` leak.
- Known accepted deviation: 320px touch-target width (~30-34px vs. 44px) is not fixed; if a future phase revisits the toolbar CSS, this remains open.

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*
