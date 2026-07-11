---
phase: 70-dm-profile-shop-fields
plan: 02
subsystem: ui
tags: [markdown, easymde, shop, razor, javascript]

# Dependency graph
requires:
  - phase: 66-quest-description-editor
    provides: shared _MarkdownEditor.cshtml partial, initMarkdownEditor, POST /markdown/preview
provides:
  - textarea.easyMDE handle exposed by markdown-editor.js's eager-init loop
  - Shop Item Description write-form wiring (Create/Create.Mobile/Edit/Edit.Mobile) for the shared Markdown editor
  - Live-editor-content reads in Create.cshtml/Edit.cshtml's bespoke inline submit validators
affects: [70-03-shop-item-description-rendering, 70-04-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bespoke inline submit validators read description via `descriptionEl.easyMDE ? descriptionEl.easyMDE.value() : descriptionEl.value` (live-editor-first, raw-textarea fallback)"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/js/markdown-editor.js
    - QuestBoard.Service/Views/ShopManagement/Create.cshtml
    - QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml
    - QuestBoard.Service/Views/ShopManagement/Edit.cshtml
    - QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml

key-decisions:
  - "textarea.easyMDE is assigned only in the eager-init DOMContentLoaded loop, not inside initMarkdownEditor itself, keeping the constructor function's signature/behavior fully unchanged for every other consumer (Quest/Character/Contact/Bio editors)"
  - "Edit.cshtml and Edit.Mobile.cshtml backfill the Create forms' placeholder text (they had none before) per the plan's explicit instruction to close the pre-existing 2-of-4 placeholder inconsistency"

patterns-established: []

requirements-completed: [PROFILEMD-02]

coverage:
  - id: D1
    description: "markdown-editor.js exposes each built EasyMDE instance on textarea.easyMDE (additive; initMarkdownEditor itself unchanged)"
    requirement: "PROFILEMD-02"
    verification:
      - kind: other
        ref: "node --check QuestBoard.Service/wwwroot/js/markdown-editor.js; grep -c 'textarea.easyMDE = initMarkdownEditor' returns 1; grep -c 'return easyMDE' returns 1; grep -c 'codemirror.save()' returns 1"
        status: pass
    human_judgment: false
  - id: D2
    description: "All four Shop write forms (Create, Create.Mobile, Edit, Edit.Mobile) render the shared _MarkdownEditor partial for Description with Required=true (red asterisk) and the shared placeholder, loading _QuestFormScripts"
    requirement: "PROFILEMD-02"
    verification:
      - kind: other
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj (0 errors); grep assertions on all 4 files for RenderPartialAsync(\"_MarkdownEditor\"), Required = true, placeholder text, shared namespace import, _QuestFormScripts.cshtml, absence of asp-for=\"Description\""
        status: pass
    human_judgment: false
  - id: D3
    description: "Create.cshtml/Edit.cshtml inline submit validators read the live editor value via textarea.easyMDE.value() with a raw-textarea fallback; 20-char guard and alert copy unchanged"
    requirement: "PROFILEMD-02"
    verification:
      - kind: other
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj (0 errors); grep -c 'easyMDE.value()' == 1 and grep -c 'description.length < 20' == 1 and grep -c 'at least 20 characters' == 1 on both Create.cshtml and Edit.cshtml"
        status: pass
    human_judgment: false
  - id: D4
    description: "Live desktop + 320px mobile confirmation of the Description editor, required asterisk, Preview toggle, and 20-char required-field regression"
    verification: []
    human_judgment: true
    rationale: "Deferred by the plan itself to the Wave-2 verification plan (70-04); requires live-browser rendering judgment this executor cannot perform headlessly."

# Metrics
duration: 20min
completed: 2026-07-10
status: complete
---

# Phase 70 Plan 02: Shop Item Description Markdown Editor Summary

**Wired the shared EasyMDE Markdown editor into Shop Item Description on all four ShopManagement write forms, and taught the two bespoke inline submit validators to read the live editor content via a new additive `textarea.easyMDE` handle.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-10T17:28:26Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments
- `markdown-editor.js`'s eager-init loop now stores each built EasyMDE instance on `textarea.easyMDE`, purely additive (nothing else reads it yet except this plan's own validators)
- Shop Item Description on Create, Create.Mobile, Edit, and Edit.Mobile now renders the shared `_MarkdownEditor` partial with `Required = true` (first required field this milestone has migrated), the shared placeholder copy, and loads `_QuestFormScripts.cshtml` for the antiforgery token + submit-sync
- The two Edit forms (which never had a placeholder) now show the same placeholder text as Create, closing a pre-existing inconsistency per the plan's explicit instruction
- `Create.cshtml`/`Edit.cshtml`'s bespoke inline `<script>` submit validators now read `Description` from `textarea.easyMDE.value()` (falling back to the raw `.value` if the editor never initialized), so the existing 20-character-minimum client-side check keeps working against the live editor content instead of the now-hidden raw textarea

## Task Commits

Each task was committed atomically:

1. **Task 1: Expose each built EasyMDE instance on its textarea element in markdown-editor.js** - `50a94791` (feat)
2. **Task 2: Wire the Markdown editor into Shop Item Description on all four write forms** - `00864b04` (feat)
3. **Task 3: Fix the bespoke inline submit validators to read the live editor value** - `c22da8e5` (fix)

_Note: this plan has no `checkpoint:human-verify` task — Wave-2's 70-04 plan owns live desktop/mobile confirmation per the plan's own `<verification>` section._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/js/markdown-editor.js` - Eager-init loop now assigns the built EasyMDE instance onto `textarea.easyMDE`
- `QuestBoard.Service/Views/ShopManagement/Create.cshtml` - Description wired to `_MarkdownEditor` (Required=true), `_QuestFormScripts` loaded, submit validator reads live editor content
- `QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml` - Description wired to `_MarkdownEditor` (Required=true), `_QuestFormScripts` loaded
- `QuestBoard.Service/Views/ShopManagement/Edit.cshtml` - Description wired to `_MarkdownEditor` (Required=true, backfilled placeholder), `_QuestFormScripts` loaded, submit validator reads live editor content
- `QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml` - Description wired to `_MarkdownEditor` (Required=true, backfilled placeholder), `_QuestFormScripts` loaded

## Decisions Made
- Kept `initMarkdownEditor`'s own signature/body completely untouched; the new `textarea.easyMDE` assignment lives only in the `DOMContentLoaded` eager-init loop, so no other consumer of `initMarkdownEditor` (Quest/Character/Contact/Bio editors) is affected.
- Followed the plan's explicit instruction to backfill the Description placeholder onto the two Edit forms even though they never had one before — an intentional, plan-directed closing of a pre-existing 2-of-4 inconsistency, not scope creep.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria in all three tasks passed as specified, including the full-phase `Html.Raw`-absence check across all four write forms.

One informational note (not a deviation, no code changed): Task 1's acceptance criterion `grep -c 'offsetParent' markdown-editor.js` returns 1 was already inconsistent with the file *before* this plan touched it — the pre-existing visibility-guard comment on the line above the guard itself already mentions "offsetParent" in prose, so the file had 2 matching lines pre-plan and still has 2 post-plan. The actual intent of that criterion (the visibility guard code is untouched) holds; `if (textarea.offsetParent === null)` is byte-identical to before this plan.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 70-03 (Shop Item Description read-side rendering via `Html.Markdown()`) can proceed independently — this plan only touched the write forms, no read views.
- Plan 70-04 (Wave-2 verification) has the four write forms ready for live desktop + 320px mobile confirmation of the Description editor, required asterisk, Preview toggle, and the 20-character regression check.
- No blockers.

---
*Phase: 70-dm-profile-shop-fields*
*Completed: 2026-07-10*

## Self-Check: PASSED

All 5 modified/created source files confirmed present on disk; all 4 commit hashes (50a94791, 00864b04, c22da8e5, 983265c3) confirmed present in git log.
