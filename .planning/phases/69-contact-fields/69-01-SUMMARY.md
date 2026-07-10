---
phase: 69-contact-fields
plan: 01
subsystem: ui
tags: [markdown, easymde, razor, contacts]

# Dependency graph
requires:
  - phase: 66-quest-description-markdown
    provides: shared _MarkdownEditor.cshtml partial, MarkdownEditorViewModel, markdown-editor.js eager-init loop, POST /markdown/preview
provides:
  - Optional ElementId override on MarkdownEditorViewModel so multiple editor instances can render on one page while sharing a FieldName
  - display:none-aware eager-init guard in markdown-editor.js (offsetParent check) so hidden editor instances defer to a later reveal handler
  - Contact Description authoring UI (Create/Create.Mobile/Edit/Edit.Mobile) using the shared EasyMDE editor
affects: [69-02-contact-notes-and-details, 69-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ElementId-aware _MarkdownEditor.cshtml: id derived from Model.ElementId ?? FieldName, letting the same partial render multiple instances of one field name on a page (needed by per-note inline editors in 69-02)"

key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs
    - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
    - QuestBoard.Service/wwwroot/js/markdown-editor.js
    - QuestBoard.Service/Views/Contacts/Create.cshtml
    - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml

key-decisions:
  - "ElementId is additive/nullable; every existing call site (Quest, Character) implicitly passes null, so the id-derivation fallback and behavior are unchanged for them"
  - "markdown-editor.js's offsetParent === null check runs only in the DOMContentLoaded eager-init loop; initMarkdownEditor itself is untouched so a future lazy-init caller still gets a full EasyMDE instance back"
  - "Applied the 'Brief contact description' placeholder to Edit/Edit.Mobile (which had none before), closing a pre-existing 2-of-4 inconsistency as an intentional byproduct, matching Character's Phase 68 precedent"

patterns-established:
  - "Mechanical field-wiring pattern (per-view: add @using ViewModels.Shared, replace plain textarea with _MarkdownEditor partial call, prepend _QuestFormScripts.cshtml to @section Scripts) now proven on a third feature (Quest, Character, Contact)"

requirements-completed: [CONTACTMD-01]

coverage:
  - id: D1
    description: "MarkdownEditorViewModel exposes an optional ElementId that overrides the derived DOM id while FieldName still drives the posted field name"
    requirement: CONTACTMD-01
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj (compiles)"
        status: pass
      - kind: other
        ref: "grep 'Model.ElementId ??' _MarkdownEditor.cshtml; grep 'name=\"@Model.FieldName\"' _MarkdownEditor.cshtml"
        status: pass
    human_judgment: false
  - id: D2
    description: "markdown-editor.js skips eager-init of any textarea whose container is display:none (offsetParent === null), leaving it for a later reveal handler"
    requirement: CONTACTMD-01
    verification:
      - kind: other
        ref: "grep 'offsetParent' QuestBoard.Service/wwwroot/js/markdown-editor.js"
        status: pass
    human_judgment: true
    rationale: "The guard's actual runtime effect (a hidden textarea not eager-initializing, a visible one still eager-initializing) is a browser DOM behavior not exercised by any existing automated test; visual/functional confirmation belongs to 69-02, which is the first plan to actually render a hidden instance."
  - id: D3
    description: "Contact Description on Create/Create.Mobile/Edit/Edit.Mobile renders the shared EasyMDE editor (toolbar + Preview) instead of a plain textarea, loading _QuestFormScripts.cshtml, with the crop UI left intact"
    requirement: CONTACTMD-01
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactsControllerIntegrationTests (30/30 pass)"
        status: pass
      - kind: other
        ref: "grep RenderPartialAsync(\"_MarkdownEditor\"/QuestBoard.Service.ViewModels.Shared/_QuestFormScripts.cshtml/asp-for=\"Description\" across all 4 files"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-07-10
status: complete
---

# Phase 69 Plan 01: Contact Description Markdown Editor + Multi-Instance Infrastructure Summary

**Contact Description authoring wired to the shared EasyMDE Markdown editor on all four write forms, plus an additive ElementId override and hidden-textarea eager-init guard that make the shared partial/JS safe for the multi-instance per-note editors 69-02 will add.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-10T12:52:00Z (approx)
- **Completed:** 2026-07-10T13:17:26Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- `MarkdownEditorViewModel` gained an optional `ElementId` property that overrides the derived DOM id while `FieldName` (and therefore POST binding) is untouched — the foundation 69-02's per-note inline editors need to render multiple instances of the same field name on one page.
- `_MarkdownEditor.cshtml`'s id derivation now prefers `Model.ElementId`, falling back to the existing `FieldName`-derived id; the textarea's `name` attribute and every other line are byte-for-byte unchanged.
- `markdown-editor.js`'s `DOMContentLoaded` eager-init loop now skips any textarea whose `offsetParent` is `null` (a `display:none` ancestor), leaving it for a later reveal handler to lazy-init — `initMarkdownEditor` itself is unmodified and still returns the constructed `EasyMDE` instance.
- Contact Create, Create.Mobile, Edit, and Edit.Mobile all render the shared `_MarkdownEditor` partial for Description (Label "Description", Placeholder "Brief contact description", Required=false) instead of a plain textarea, and each loads `_QuestFormScripts.cshtml` (EasyMDE CDN assets + antiforgery token) ahead of the existing image-crop scripts.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ElementId override to MarkdownEditorViewModel + _MarkdownEditor.cshtml, and a visibility guard to markdown-editor.js** - `e8971c8` (feat)
2. **Task 2: Wire the Markdown editor into Contact Description on all four write forms** - `fae86b8` (feat)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/Shared/MarkdownEditorViewModel.cs` - new optional `ElementId` property
- `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` - `elementId` derivation now ElementId-aware
- `QuestBoard.Service/wwwroot/js/markdown-editor.js` - eager-init loop skips `display:none` textareas
- `QuestBoard.Service/Views/Contacts/Create.cshtml` - Description wired to `_MarkdownEditor`, loads `_QuestFormScripts`
- `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml` - same
- `QuestBoard.Service/Views/Contacts/Edit.cshtml` - same, plus placeholder added (previously had none)
- `QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml` - same, plus placeholder added (previously had none)

## Decisions Made
- ElementId is additive/nullable, so every existing single-instance call site (Quest Description/Rewards/Recap, Character Description/Backstory) is unaffected — they implicitly pass `ElementId = null` and remain visible at load, so they still eager-init exactly as before.
- Applying the "Brief contact description" placeholder to the two Edit forms (which had none previously) was intentional per the plan, matching Character's Phase 68 precedent of closing pre-existing minor inconsistencies as a byproduct of the mechanical wiring pass.

## Deviations from Plan

None - plan executed exactly as written. All acceptance-criteria greps and build/test verification steps passed as specified.

## Issues Encountered

None. One cosmetic note: the plan's acceptance-criteria grep for `offsetParent` expected a literal count of 1, but the added guard also includes an explanatory inline comment mentioning `offsetParent`, so a plain `grep -c` returns 2 lines matched. This does not affect correctness — `Grep` tool confirmed the actual code guard (`if (textarea.offsetParent === null) { return; }`) is present exactly once and behaves as specified.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The `ElementId` override and hidden-textarea eager-init guard are now in place and unit-verified via build; 69-02 (Contact Notes + Details rendering) can safely render multiple hidden `_MarkdownEditor` instances per note on one page.
- Contact Description write-side (CONTACTMD-01) is fully delivered; the read half (Details rendering of Description as formatted HTML) is explicitly deferred to 69-02 per this plan's scope.
- No blockers.

---
*Phase: 69-contact-fields*
*Completed: 2026-07-10*

## Self-Check: PASSED

All 7 files listed under Files Created/Modified plus this SUMMARY.md were confirmed present on disk. All 3 commits (`e8971c8`, `fae86b8`, `d09d1bd`) confirmed present in git log.
