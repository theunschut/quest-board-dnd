---
phase: 68-character-fields
plan: 01
subsystem: character-write-forms
tags: [markdown, editor, characters, css-specificity]
dependency-graph:
  requires: []
  provides:
    - "Character Description/Backstory routed through _MarkdownEditor on all 4 write forms"
    - "Tripled-class markdown-editor.css override (protects any future :not()-qualified card catch-all, e.g. Phase 69 Contacts)"
  affects:
    - "QuestBoard.Service/Views/Characters/Create.cshtml"
    - "QuestBoard.Service/Views/Characters/Create.Mobile.cshtml"
    - "QuestBoard.Service/Views/Characters/Edit.cshtml"
    - "QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml"
    - "QuestBoard.Service/wwwroot/css/markdown-editor.css"
tech-stack:
  added: []
  patterns:
    - "Full-path ~/Views/Quest/_QuestFormScripts.cshtml include inside an existing @section Scripts block, for a controller folder other than Quest (mirrors Phase 67's EditRecap precedent)"
key-files:
  created: []
  modified:
    - "QuestBoard.Service/Views/Characters/Create.cshtml"
    - "QuestBoard.Service/Views/Characters/Create.Mobile.cshtml"
    - "QuestBoard.Service/Views/Characters/Edit.cshtml"
    - "QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml"
    - "QuestBoard.Service/wwwroot/css/markdown-editor.css"
decisions: []
metrics:
  duration: "~3 minutes"
  completed: "2026-07-10"
status: complete
---

# Phase 68 Plan 01: Character Write-Form Markdown Editor Wiring Summary

Wired the shared EasyMDE Markdown editor (toolbar, Preview toggle, paragraph-break hint) into Character Description and Backstory on all four Character write forms, and hardened the global editor text-color CSS override so it survives Character mobile's stricter `:not(.badge)` catch-all.

## What Was Built

- **Create.cshtml + Create.Mobile.cshtml** — replaced the plain `<textarea asp-for="Description">`/`<textarea asp-for="Backstory">` blocks with two `_MarkdownEditor` partial calls each (bare `FieldName`, `Required = false`, UI-SPEC placeholder copy with trailing ellipsis). Added `@using QuestBoard.Service.ViewModels.Shared` and inserted `@{ await Html.RenderPartialAsync("~/Views/Quest/_QuestFormScripts.cshtml"); }` into each file's existing `@section Scripts` block so the EasyMDE CDN assets, antiforgery token, and `markdown-editor.js` init actually load (the plan's objective flagged this as a correction to 68-PATTERNS.md, which incorrectly claimed the scripts were global).
- **Edit.cshtml + Edit.Mobile.cshtml** — identical wiring. Desktop `Edit.cshtml` previously had zero `placeholder=` attributes on either field (a pre-existing 1-of-4 gap versus the other three forms); routing through `_MarkdownEditor` closed that gap as a byproduct.
- **markdown-editor.css** — tripled the `.CodeMirror`/`.editor-preview` class selectors (`.CodeMirror.CodeMirror.CodeMirror`, etc.) so the black-text override beats `character-form.mobile.css`'s `.character-form-card span:not(.badge)` catch-all, which ties the doubled-class version on specificity for `<span>` syntax-highlighting nodes. Updated the file's explanatory comment to describe the new `:not()`-qualified-catch-all rationale, with no phase/requirement-ID references per CLAUDE.md.

## Deviations from Plan

None — plan executed exactly as written. All acceptance criteria verified via `dotnet build` (0 errors/warnings) and targeted grep checks confirming: two `_MarkdownEditor` calls per file, exactly one `@section Scripts` block per file with exactly one `_QuestFormScripts` include, no leftover `<textarea asp-for="Description"`/`Backstory` markup, both placeholders carry the trailing ellipsis, and no `text-danger">*` asterisk was added next to either label.

## Verification

- `dotnet build QuestBoard.Service/QuestBoard.Service.csproj -c Debug` — 0 errors, 0 warnings, after each task and again at the end.
- `grep -Ec "\.CodeMirror\.CodeMirror\.CodeMirror|\.editor-preview\.editor-preview\.editor-preview" markdown-editor.css` → 5 matches (comment + 4 selector lines), exceeding the required minimum of 4.
- Manual grep sweep across all 4 write forms confirmed the exact acceptance-criteria shape (imports, partial calls, single Scripts section, single script include, no stray asterisks).

## Known Stubs

None.

## Threat Flags

None — this plan touches only client-side editor chrome on an existing trust boundary (Character write-form POST) and reuses the already-vetted `POST /markdown/preview` endpoint verbatim. See the plan's own `<threat_model>` section for the full STRIDE register (all dispositions `mitigate`/`accept`, no new surface).

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Characters/Create.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Create.Mobile.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Edit.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
- FOUND: QuestBoard.Service/wwwroot/css/markdown-editor.css
- FOUND commit f92788b (feat(68-01): wire Markdown editor into Character Create forms)
- FOUND commit de11060 (feat(68-01): wire Markdown editor into Character Edit forms)
- FOUND commit 6e201ec (fix(68-01): triple markdown editor text-color override specificity)
