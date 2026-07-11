---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 05
subsystem: ui
tags: [easymde, markdown, razor-views, antiforgery, quest-forms]

# Dependency graph
requires:
  - phase: 66-quest-description-editor-rendering-proof-of-concept
    provides: "POST /markdown/preview endpoint (66-02) that markdown-editor.js calls"
  - phase: 66-quest-description-editor-rendering-proof-of-concept
    provides: "_MarkdownEditor.cshtml partial + MarkdownEditorViewModel + markdown-editor.js + EasyMDE-ready CSS (66-04)"
provides:
  - "_QuestFormScripts.cshtml generalized to load EasyMDE CDN assets, expose window.markdownAntiforgeryToken via Antiforgery.GetAndStoreTokens, and include markdown-editor.js — while preserving the existing proposed-dates validation script"
  - "All six quest write views (Create/Edit/CreateFollowUp x desktop/mobile) render Quest Description through the shared _MarkdownEditor partial instead of a plain textarea"
  - "Both Follow-Up views (previously with no shared script include at all) now render _QuestFormScripts at the end of their existing single @section Scripts block"
affects: [66-06-quest-read-rendering, 66-07-manual-ui-checkpoint]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-form antiforgery token minted via Antiforgery.GetAndStoreTokens(ViewContext.HttpContext) inside the shared script partial, assigned to window.markdownAntiforgeryToken before markdown-editor.js runs"
    - "Follow-Up views' single @section Scripts block appends a RenderPartialAsync(\"_QuestFormScripts\") call after their own inline addDate/removeDate script, since Razor only allows one Scripts section per view"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
    - QuestBoard.Service/Views/Quest/Create.cshtml
    - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Edit.cshtml
    - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
    - QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml

key-decisions:
  - "Create/Create.Mobile/CreateFollowUp/CreateFollowUp.Mobile pass FieldName=\"Description\" (top-level model binding); Edit/Edit.Mobile pass FieldName=\"Quest.Description\" (nested EditQuestViewModel binding) — matching each view's pre-existing asp-for expression exactly."
  - "Rewards textarea left untouched in all views that have it (Create/Edit/CreateFollowUp, desktop+mobile) — Rewards markdown support is explicitly out of scope until Phase 67."

requirements-completed: [EDITOR-01, EDITOR-02, EDITOR-06, QUESTMD-01]

# Metrics
duration: ~20min
completed: 2026-07-09
status: complete
---

# Phase 66 Plan 05: Quest Form Editor Wiring Summary

**All six Quest write views (Create/Edit/Follow-Up x desktop/mobile) now render the EasyMDE toolbar editor for Description via a generalized `_QuestFormScripts.cshtml` that loads EasyMDE (CDN+SRI), exposes a per-form antiforgery token, and includes `markdown-editor.js` — extended for the first time onto the two Follow-Up forms, which previously had no shared script wiring at all.**

## Performance

- **Duration:** ~20 min
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- `_QuestFormScripts.cshtml` now loads the EasyMDE CDN stylesheet + script (version-pinned, SRI-verified), mints a per-form antiforgery token via `Antiforgery.GetAndStoreTokens(ViewContext.HttpContext)` into `window.markdownAntiforgeryToken`, and includes `~/js/markdown-editor.js` — in an order that guarantees EasyMDE is defined before init and the token exists before `DOMContentLoaded`. The pre-existing proposed-dates validation script is untouched.
- Create.cshtml, Create.Mobile.cshtml, Edit.cshtml, Edit.Mobile.cshtml all render `_MarkdownEditor` for their Description field, each passing the exact model-binding expression (`Description` or `Quest.Description`) that the original `<textarea asp-for="...">` used.
- CreateFollowUp.cshtml and CreateFollowUp.Mobile.cshtml — which previously had their own standalone `@section Scripts` block with no shared quest-form wiring — now also render `_MarkdownEditor` for Description and append `RenderPartialAsync("_QuestFormScripts")` to the end of their existing Scripts section, bringing EasyMDE support to Follow-Up quest creation for the first time.
- `dotnet build QuestBoard.Service` succeeds with 0 warnings/errors after every task.

## Task Commits

Each task was committed atomically:

1. **Task 1: Generalize _QuestFormScripts.cshtml to load EasyMDE assets + token + markdown-editor.js** - `c2512363` (feat)
2. **Task 2: Swap Description textarea for the editor partial on Create/Edit (desktop + mobile)** - `e80c34d7` (feat)
3. **Task 3: Swap Description on Follow-Up forms + include _QuestFormScripts inside their existing Scripts block** - `3c8f7b85` (feat)

_Note: This plan's SUMMARY/state commit is made separately by the orchestrator after all worktree agents in the wave complete._

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml` - Added EasyMDE CDN CSS/JS (SRI), antiforgery token exposure, markdown-editor.js include
- `QuestBoard.Service/Views/Quest/Create.cshtml` - Description now renders via `_MarkdownEditor` (FieldName=`Description`)
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` - Same swap, mobile layout
- `QuestBoard.Service/Views/Quest/Edit.cshtml` - Description now renders via `_MarkdownEditor` (FieldName=`Quest.Description`)
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` - Same swap, mobile layout
- `QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml` - Description swap + `_QuestFormScripts` appended to existing Scripts section
- `QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml` - Same, mobile layout

## Decisions Made
- Field-name mapping followed the plan's locked interface facts exactly: top-level `Description` for Create/CreateFollowUp (both breakpoints), `Quest.Description` for Edit (both breakpoints) — verified by grep that no `<textarea asp-for="Description">` or `<textarea asp-for="Quest.Description">` remains anywhere in the six views.
- Follow-Up views' pre-existing addDate/removeDate inline script and their own single `@section Scripts` block were preserved verbatim; `_QuestFormScripts` was appended at the end rather than replacing anything, per the plan's explicit "Razor allows only one @section Scripts per view" constraint.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All 3 write-form families (Create/Edit/Follow-Up), both desktop and mobile, now load EasyMDE via the shared `_QuestFormScripts` partial and render Description through `_MarkdownEditor`, wired to the `/markdown/preview` endpoint (66-02) via `window.markdownAntiforgeryToken`.
- Full visual/behavioral confirmation (toolbar renders, preview round-trips correctly, no console errors) is deferred to 66-07's manual UI checkpoint, per this plan's own `<verification>` block — this plan's scope was pure server-side/Razor wiring, not live-browser verification.
- No blockers for 66-06 (read-side rendering) or 66-07.

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 7 modified source files and the SUMMARY.md confirmed present on disk; all 4 commits (`c2512363`, `e80c34d7`, `3c8f7b85`, `fa2095a8`) confirmed present in git log.
