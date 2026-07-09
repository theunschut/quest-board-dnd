---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 04
subsystem: ui
tags: [easymde, markdown, bootstrap-tooltip, font-awesome, css]

# Dependency graph
requires:
  - phase: 65-markdown-rendering-foundation
    provides: IMarkdownService.RenderToHtml(string?, MarkdownRenderTarget) used by the /markdown/preview endpoint this JS module calls
provides:
  - Shared EasyMDE init module (markdown-editor.js) — class-selector-driven, async server-round-trip preview
  - Touch-target/fit CSS (markdown-editor.css) — 44px buttons, 0-gap toolbar, sizing-only per D-03
  - Rendered-Markdown typography CSS (markdown-content.css) — heading collapse, blockquote accent, parchment-gold color
  - Shared write-side partial (_MarkdownEditor.cshtml) + MarkdownEditorViewModel — field-name-explicit contract reusable across all 3 write forms
  - FA v4-shim + both new stylesheets loaded globally in _Layout.cshtml and _Layout.Mobile.cshtml
  - App-wide Bootstrap tooltip init in site.js (first tooltip usage in this codebase)
affects: [66-05-quest-form-wiring, 66-06-quest-read-rendering, 67-quest-rewards-recap, 68-character, 69-contact, 70-dm-profile-shop]

# Tech tracking
tech-stack:
  added: [EasyMDE 2.21.0 (client asset consumption target — CDN script/link tags added in 66-05), Font Awesome v4-shims 6.4.0]
  patterns:
    - "Class-selector-driven JS init (querySelectorAll('.markdown-editor-textarea'), never getElementById) because asp-for generates different ids per form"
    - "Shared editor partial takes explicit FieldName instead of asp-for, so one partial serves fields with differing model paths"
    - "Third-party widget CSS overrides scoped narrowly to sizing/fit only, mirroring the image-crop.css precedent — no restyling of default chrome"

key-files:
  created:
    - QuestBoard.Service/wwwroot/js/markdown-editor.js
    - QuestBoard.Service/wwwroot/css/markdown-editor.css
    - QuestBoard.Service/wwwroot/css/markdown-content.css
    - QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs
    - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/wwwroot/js/site.js

key-decisions:
  - "Blockquote toolbar button uses the literal string 'quote' (EasyMDE's internal name), not 'blockquote' — a locked interface fact from the plan, verified present in markdown-editor.js"
  - "FA v4-shims added globally in both layouts (not per-view like Cropper.js) since Font Awesome itself is already loaded globally"
  - "markdown-content.css headings intentionally override the global site.css h1-h6 color rule within .markdown-content scope, reusing quest-description-box's exact color/text-shadow so headings stay legible on the dark overlay box"

patterns-established:
  - "Pattern: shared *-textarea class + querySelectorAll for any future field-editor JS wiring (67-70 will reuse this exact module, no per-field JS needed)"
  - "Pattern: MarkdownEditorViewModel + _MarkdownEditor.cshtml partial is the reusable write-side contract for every future Markdown-enabled field"

requirements-completed: [EDITOR-01, EDITOR-02, EDITOR-03, EDITOR-05, EDITOR-06]

# Metrics
duration: ~10min
completed: 2026-07-09
status: complete
---

# Phase 66 Plan 04: Shared Markdown Editor Client Assets Summary

**EasyMDE init module + touch-target/fit CSS + rendered-content typography CSS + shared editor partial/view model + FA v4-shim + app-wide Bootstrap tooltip init — all wiring 66-05 (form consumption) and 66-06 (read rendering) will build on directly.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-07-09T15:06:22Z
- **Tasks:** 3
- **Files modified:** 8 (5 created, 3 modified)

## Accomplishments
- `markdown-editor.js` initializes an EasyMDE instance on every `.markdown-editor-textarea` found on the page, wiring the 7-button toolbar (`bold, italic, heading, unordered-list, link, quote, preview`) and an async server-round-trip `previewRender` that POSTs to `/markdown/preview` with the antiforgery header, falling back to a `text-danger` error message on failure.
- `markdown-editor.css` overrides only `min-width`/`min-height`/`margin` on toolbar buttons and `padding` on the toolbar wrapper — 44px touch targets with 0 gap so all 7 buttons fit one 320px-wide row, with zero color/border/icon changes (D-03 compliant).
- `markdown-content.css` scopes all rendered-Markdown typography under `.markdown-content`: body text, bold weight, a single collapsed heading tier at parchment-gold (`#F4E4BC`) matching `.quest-description-box`'s exact text-shadow, and a gold-accented blockquote — deliberately overriding the global `site.css` h1-h6 rule within this scope only.
- FA v4-shims (SRI-pinned) and both new stylesheets now load globally in `_Layout.cshtml` and `_Layout.Mobile.cshtml`, immediately after the existing FA6/image-crop.css links.
- `MarkdownEditorViewModel` + `_MarkdownEditor.cshtml` give all 3 future write forms (66-05) a single partial: label, info-circle tooltip hint with the exact locked wording, an editor-ready textarea targeted by class (no `asp-for`, explicit `name`/`id` instead), and a validation span reproducing `asp-validation-for` behavior.
- `site.js` gained the app's first Bootstrap tooltip init — a `DOMContentLoaded`-scoped loop over `[data-bs-toggle="tooltip"]`, added alongside the existing toast-init block.

## Task Commits

Each task was committed atomically:

1. **Task 1: markdown-editor.js + markdown-editor.css** - `97332d68` (feat)
2. **Task 2: markdown-content.css + FA v4-shim/CSS links in both layouts** - `6343aca4` (feat)
3. **Task 3: MarkdownEditorViewModel + _MarkdownEditor.cshtml + tooltip init** - `f7bea1a9` (feat)

_Note: This plan's SUMMARY/state commit is made separately by the orchestrator after all worktree agents in the wave complete._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/js/markdown-editor.js` - Class-selector-driven EasyMDE init + async server preview
- `QuestBoard.Service/wwwroot/css/markdown-editor.css` - 44px touch-target + 0-gap toolbar sizing override only
- `QuestBoard.Service/wwwroot/css/markdown-content.css` - Rendered-Markdown typography (heading collapse, blockquote, gold color)
- `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs` - FieldName/Value/Label/Required/Placeholder contract
- `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` - Shared write-side partial (label + info hint + editor textarea + validation)
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Added FA v4-shims link + markdown-editor.css/markdown-content.css links
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Same three additions as desktop layout
- `QuestBoard.Service/wwwroot/js/site.js` - Added one-time Bootstrap tooltip init loop

## Decisions Made
- Followed the plan's locked interface facts verbatim: `"quote"` (not `"blockquote"`) toolbar entry, class-selector (not `getElementById`) init, exact tooltip wording, FA v4-shims SRI hash as given in the plan.
- No `Program.cs` or antiforgery configuration changes — this plan only builds the client asset layer; the `/markdown/preview` endpoint itself and the antiforgery token wiring into the JS module (`window.markdownAntiforgeryToken`) are 66-05's responsibility, consistent with the plan's stated scope ("form wiring (66-05) and read rendering (66-06) are pure consumption").

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 5 locked "must_haves" artifacts exist and pass their grep-based acceptance criteria (verified individually per task and via the plan's overall `<verification>` block).
- `dotnet build QuestBoard.Service` succeeds with 0 warnings/errors after both build-gated tasks (2 and 3).
- 66-05 can now include `_MarkdownEditor.cshtml` (passing a `MarkdownEditorViewModel`) on Create/Edit/CreateFollowUp forms, add the EasyMDE CDN `<script>`/`<link>` tags (this plan intentionally did not add them, per the RESEARCH.md note that they belong to 66-05), and set `window.markdownAntiforgeryToken` before `markdown-editor.js` runs.
- 66-06 can now call the new `.markdown-content` CSS scope's typography directly once `Html.Markdown()` (a 66-06 concern per RESEARCH.md's project structure) wraps rendered output in a `.markdown-content` div.
- Visual verification of the FA v4-shim (icons rendering, not blank/tofu boxes) and the 320px single-row toolbar fit is deferred to whichever plan first renders a live EasyMDE instance (66-05), since this plan ships no page that actually invokes `initMarkdownEditor` yet — no `.markdown-editor-textarea` exists in any view until 66-05 wires the partial in.

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*
