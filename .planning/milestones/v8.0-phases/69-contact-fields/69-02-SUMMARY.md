---
phase: 69-contact-fields
plan: 02
subsystem: ui
tags: [markdown, easymde, razor, contacts, notes]

# Dependency graph
requires:
  - phase: 69-01-contact-description-markdown-editor
    provides: MarkdownEditorViewModel.ElementId override, markdown-editor.js display:none-aware eager-init guard, Contact Description write forms already wired to _MarkdownEditor
provides:
  - Contact Description rendered as sanitized formatted HTML on Contact Details (desktop + mobile), completing CONTACTMD-01
  - Add Note + per-note Edit forms wired to the shared EasyMDE editor via _MarkdownEditor, completing CONTACTMD-02's write side
  - Per-note independent Html.Markdown(note.Text) rendering (one call per note, no cross-note formatting bleed)
  - D-03 auto-collapse note-editor registry (noteEditors Map + collapseNote()) with lazy-init-on-first-click and unsaved-text discard
  - Mobile CSS list-item companion edits (li added to Description catch-all and .note-item scope-out) for Markdown list contrast
affects: [69-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "First Details/read page in the app to load _QuestFormScripts.cshtml (EasyMDE CDN + markdown-editor.js + antiforgery token) — inline per-note editing on a read page, not a dedicated Create/Edit route"
    - "Per-note multi-instance editor registry (noteEditors Map keyed by data-note-id) driving D-03 mutual-exclusivity + lazy-init-on-reveal, sidestepping the CodeMirror hidden-container sizing bug by construction"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css

key-decisions:
  - "FieldName stayed literally \"Text\" on every note's Edit form (never made per-note-unique) so EditNote's unprefixed model binding keeps resolving viewModel.Text — only ElementId (DOM id, not POST field name) is per-note-unique (Text_{note.Id})"
  - "EasyMDE for a note is constructed only on the note's first Edit-button click, after its container is already display:block, never eagerly at DOMContentLoaded — avoids the CodeMirror hidden-container sizing bug entirely instead of requiring a correctly-timed .refresh() call"
  - "D-03 auto-collapse discards unsaved text silently (no confirm dialog), matching 69-CONTEXT.md's explicit low-stakes design choice for a short note edit"

patterns-established:
  - "Per-note-unique DOM id / shared POST field name split (ElementId vs FieldName) is now proven end-to-end as the pattern for any future N-instances-of-one-field-on-one-page editor need"

requirements-completed: [CONTACTMD-01, CONTACTMD-02]

coverage:
  - id: D1
    description: "Contact Description renders as sanitized formatted HTML via Html.Markdown() on both Contact Details desktop and mobile, replacing the pre-wrap plain-text paragraph"
    requirement: CONTACTMD-01
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj (compiles)"
        status: pass
      - kind: other
        ref: "grep -c 'Html.Markdown(Model.Description)' Details.cshtml / Details.Mobile.cshtml == 1 each"
        status: pass
    human_judgment: false
  - id: D2
    description: "Add Note and every per-note Edit form render the shared EasyMDE editor (toolbar + Preview) instead of a plain textarea; posted field name stays \"Text\", per-note DOM id is unique"
    requirement: CONTACTMD-02
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~ContactsControllerIntegrationTests (30/30 pass)"
        status: pass
      - kind: other
        ref: "grep FieldName=\"Text\" (2x), ElementId = $\"Text_{note.Id}\" (1x), _QuestFormScripts.cshtml (1x) per file — all pass"
        status: pass
    human_judgment: false
  - id: D3
    description: "Each note renders independently via its own Html.Markdown(note.Text) call inside the @foreach — one author's unclosed formatting cannot bleed into another note"
    requirement: CONTACTMD-02
    verification:
      - kind: other
        ref: "grep -c 'Html.Markdown(note.Text)' Details.cshtml / Details.Mobile.cshtml == 1 each (single call site inside the loop, not concatenated)"
        status: pass
    human_judgment: true
    rationale: "Grep confirms the structural guarantee (one call per note, never concatenated); actual cross-note formatting-isolation behavior with real unclosed-markdown content is a rendering behavior best confirmed visually, per RESEARCH.md's manual-UAT test map for CONTACTMD-02."
  - id: D4
    description: "Opening one note's editor auto-collapses any other currently-open note editor and reverts its unsaved text to the saved value (D-03); each note's EasyMDE instance is lazily created only on first Edit-button click, after its container is visible"
    requirement: CONTACTMD-02
    verification:
      - kind: other
        ref: "grep -c 'noteEditors' >=3, 'function collapseNote' ==1, 'initMarkdownEditor(textarea, window.markdownAntiforgeryToken)' ==1, 'instance.value(' ==1 per file — all pass"
        status: pass
    human_judgment: true
    rationale: "D-03 mutual exclusivity and the CodeMirror-hidden-container-avoidance are pure client-side DOM/JS interactions with no automated test infra in this codebase (matches Phase 68's precedent) — RESEARCH.md explicitly defers this to the 69-03 UAT checkpoint."
  - id: D5
    description: "Whitespace-preserving inline styles removed from Description and each note's view container; mobile CSS gained li companion edits to the Description catch-all and .note-item scope-out for Markdown list contrast"
    requirement: CONTACTMD-01
    verification:
      - kind: other
        ref: "grep -c 'pre-wrap' Details.cshtml / Details.Mobile.cshtml == 0; grep -c '.contact-detail-card li' and '.contact-detail-card .note-item li' in contact-detail.mobile.css == 1 each"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-07-10
status: complete
---

# Phase 69 Plan 02: Contact Description Read-Side + Contact Notes Markdown Editor Summary

**Contact Description now renders as sanitized HTML on Details, and every Contact Note (Add + per-note Edit) is wired to the shared EasyMDE editor with independent per-note rendering and a new D-03 auto-collapse/lazy-init registry — the first multi-instance inline Markdown editor in the app.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-07-10T15:23:00Z (approx)
- **Completed:** 2026-07-10T15:28:00Z (approx)
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- `Details.cshtml`/`Details.Mobile.cshtml` render `Model.Description` via `@Html.Markdown(Model.Description)` instead of a pre-wrap `<p>`, completing CONTACTMD-01's read side (the write side shipped in 69-01).
- The always-visible Add Note form and every per-note Edit form now render the shared `_MarkdownEditor` partial (full toolbar + Preview immediately, per D-02) instead of a bare `<textarea name="Text">`; the posted field name stays literally `"Text"` for both, while each note's Edit form gets a unique DOM id (`Text_{note.Id}`) via the `ElementId` override 69-01 added.
- Each note's view text now renders through its own `@Html.Markdown(note.Text)` call inside the `@foreach`, preserving CONTACTMD-02's per-note independent-rendering constraint by construction (never concatenated into a single string).
- The old independent-toggle note-edit script was replaced with a `noteEditors` Map + `collapseNote()` D-03 auto-collapse registry: opening one note's editor collapses and discards unsaved text in any other open note editor, and each note's EasyMDE instance is lazily created only on its first Edit-button click (after the container is already visible), sidestepping the CodeMirror hidden-container sizing bug entirely.
- Both Details views now load `_QuestFormScripts.cshtml` (EasyMDE CDN assets + `markdown-editor.js` + antiforgery token) — the first Details/read page in the app to need editor assets, since Contact Notes are edited inline rather than on a separate Create/Edit page.
- `contact-detail.mobile.css` gained two `li` companion edits (Description catch-all, `.note-item` scope-out) so Markdown lists render with correct contrast in both Description and note cards.

## Task Commits

Each task was committed atomically:

1. **Task 1: Render Contact Description as Markdown on Details (desktop + mobile)** - `c0564ce` (feat)
2. **Task 2: Wire Add Note + per-note Edit editors and per-note Markdown rendering (desktop + mobile)** - `4a16645` (feat)
3. **Task 3: Replace the note-toggle script with the D-03 auto-collapse + lazy-init registry, and add the mobile list CSS edits** - `a1a3f90` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/Contacts/Details.cshtml` - Description → `Html.Markdown()`; Add Note + per-note Edit → `_MarkdownEditor` partial; per-note view → `Html.Markdown(note.Text)`; note-toggle script → D-03 registry; loads `_QuestFormScripts.cshtml`
- `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml` - identical set of edits, byte-for-byte matching desktop's structure per the existing convention
- `QuestBoard.Service/wwwroot/css/contact-detail.mobile.css` - `li` added to the `.contact-detail-card` catch-all and to the `.note-item` scope-out rule

## Decisions Made
- `FieldName` stayed exactly `"Text"` on every note's Edit form (never made per-note-unique) so `EditNote`'s unprefixed model binding keeps resolving `viewModel.Text`; only `ElementId` (DOM id, not the POST field name) is per-note-unique.
- Per RESEARCH.md's Unknown 2 recommendation, EasyMDE for a note is constructed only on first Edit-button click, after the container is already `display:block` — never eagerly at `DOMContentLoaded` — avoiding the CodeMirror hidden-container sizing bug by construction rather than requiring a correctly-timed `.refresh()` call.
- D-03 auto-collapse discards unsaved text silently, with no confirmation dialog, matching `69-CONTEXT.md`'s explicit design choice that this is a low-stakes, easily-redone interaction.

## Deviations from Plan

None - plan executed exactly as written. All acceptance-criteria greps, `dotnet build`, and `dotnet test --filter FullyQualifiedName~ContactsControllerIntegrationTests` (30/30 passing) matched the plan's verification steps for all three tasks.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- CONTACTMD-01 is now fully complete (write side from 69-01, read side from this plan).
- CONTACTMD-02 is fully complete: Add Note + per-note Edit use the shared editor, each note renders independently, D-03 exclusivity with unsaved-text discard works, and per-note EasyMDE is lazily created only when shown.
- All whitespace-preserving inline styles are removed from Contact Description and Notes; mobile list CSS edits are in place.
- D-01 (no Index card preview), D-02 (full toolbar on Add Note immediately), and D-03 (single open note editor) are all honored.
- Remaining manual/UAT verification for this phase (per RESEARCH.md's test map and this plan's own `<verification>` block) is deferred to 69-03: visual confirmation that unclosed formatting in one note doesn't bleed into another, D-03 collapse/discard behavior in a real browser, full-width toolbar on first note-editor open, and 320px mobile toolbar fit inside `.note-item`'s nested padding.
- No blockers.

---
*Phase: 69-contact-fields*
*Completed: 2026-07-10*

## Self-Check: PASSED
