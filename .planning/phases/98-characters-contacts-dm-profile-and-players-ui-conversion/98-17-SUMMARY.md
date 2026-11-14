---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 17
subsystem: characters-forms
tags: [ui-conversion, razor, form-contract, merge, template-module, guard-tests]
requires: [98-16]
provides:
  - "Characters/Create and Characters/Edit as one view each for both layouts on the form contract (form-page wrapper, h1 title card, picture block, required markers with hidden spoken text, shared crop dialog, action row)"
  - "Views/Characters/_CharacterClassRows.cshtml: the server-rendered class rows, the one row template and the Add Another Class button, shared by both forms"
  - "wwwroot/js/character-form.js: clones the template and renumbers Classes[i] from the rows present; no markup from strings"
  - "wwwroot/css/character-form.css (pages layer, tokens only): the 200px current-picture class and the full-width Remove where the columns stack"
  - "CharacterFormRenderTests (both forms under both User-Agents), PhoneCharacterForms_MarkClassesRequired, CharacterFormModule_WritesNoMarkupFromStrings, a both-editors markup theory"
affects: [98-18, 98-23]
tech-stack:
  added: []
  patterns: [one server-rendered template cloned by a module with rewritten name indexes; one partial shared by two merged forms; stack action row on the one form whose commit label does not fit a 360 phone]
key-files:
  created:
    - QuestBoard.Service/Views/Characters/_CharacterClassRows.cshtml
    - QuestBoard.Service/wwwroot/js/character-form.js
    - QuestBoard.Service/wwwroot/css/character-form.css
    - QuestBoard.IntegrationTests/Tests/CharacterFormRenderTests.cs
  modified:
    - QuestBoard.Service/Views/Characters/Create.cshtml
    - QuestBoard.Service/Views/Characters/Edit.cshtml
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/MarkdownEditorMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Characters/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/character-form.mobile.css
decisions:
  - "Path run: MERGE. Gate 1 answer charactersFormMerge = { merge: true, placeholders: \"create\", explicitYes: true }; the merged Edit takes Create's placeholders (Enter character name, https://...)"
  - "The row markup lives once in a shared partial (rows, template, Add button) instead of being repeated in Create and Edit; the label and the form-specific hint stay in each view"
  - "A character with no stored classes renders one empty row (no class selected, level 1) at index 0, and the module numbers new rows from the rows present, so the old duplicate index is gone"
  - "Create keeps form-actions--stack (commit first on screen on the phone, one row on desktop); Edit stays on one row, per the Gate 1 formActions answer"
  - "Focus goes to the new row's Class select after Add and to Add Another Class after Remove, so keyboard focus is never lost with a removed button"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 17: Characters Create and Edit merged onto the form contract Summary

Characters Create and Edit are one view each for the desktop and phone layouts, with the class rows cloned by a small module from one server-rendered template (index renumbered from the rows present), Classes required on both layouts, the approved image block and action row, and every guard rewritten to the same intent.

## Outcome applied

The Gate 1 answer read from `gate1/answers.json`: `charactersFormMerge`: `{ "merge": true, "placeholders": "create", "explicitYes": true }`. So the **merge path** ran (the twin path did not): `Create.Mobile.cshtml`, `Edit.Mobile.cshtml` and `character-form.mobile.css` are deleted, and the merged Edit carries Create's placeholders. Other answers used: `imageBlock` default variant, `formActions` phone one row with the stack on Characters Create only, form title gold (foundation default), exit label Cancel.

## Task 1 (tracer, commit `40f5afeb`): class rows on all four views

- `_CharacterClassRows.cshtml`: `#class-list` with the rows the character has (one empty row when none; the first has no Remove), `<template id="class-row-template">` with `Classes[__index__]` names, `aria-label` Class and Class level on every row's controls, `btn btn-danger` Remove, and Add Another Class `btn-secondary btn-sm`.
- `character-form.js`: clones the template, renumbers `Classes[i]` in `name`, `id` and `for` from the rows present after every add and remove, focuses the new row or the Add button; no markup from strings, no globals, no inline style.
- All four views then used the partial and the module, lost their inline scripts (`grep -c "<script>"` = 0 on each), and the phone views gained the Classes required marker. Name, Level and Classes carry the marker on an `aria-hidden` star plus a visually hidden ` (required)` on every view.
- Tests written first: `CharacterFormRenderTests`, `PhoneCharacterForms_MarkClassesRequired`, `CharacterFormModule_WritesNoMarkupFromStrings`; the template, row, marker and module facts passed, the layout facts waited for Task 2.
- Tracer gate: no auto flag is set and this sequential run cannot be resumed, so (as in 98-13 and 98-15) the gate was the automated re-run of the tracer's verify, then the live `class-rows.mjs` check under both User-Agents, before expansion.
- **Class-row browser check** (`after/98-17/class-rows.json`, `tools/class-rows.mjs`): on Create and on Edit of `char.nopic`, under desktop and the Pixel 7 phone User-Agent: one initial row with no Remove, add two rows, remove the middle one, posted names contiguous (`Classes[0]` and `Classes[1]`), focus lands on Add Another Class after Remove, save, reload shows the saved classes, no console errors; `char.nopic` was restored through the form to level 3 and `Wizard:3` exactly as before; the probe characters the tool created (`p98 class rows probe desktop` and `... phone`) were removed through the app's own Delete button. Result `ok`.

## Task 2 (commit `ed4f6afd`): the merge

- `Create.cshtml` and `Edit.cshtml`: `character-form-page form-page` wrapper, page-title card with one `h1` (decorative icon), validation summary in All mode with no danger class, the picture block in the left column (file input level with the Name field, hint under it), the Edit thumbnail as `img-thumbnail form-picture-thumb` under the input and its hint with Re-crop Photo (`btn-warning btn-sm`, `data-crop-original-url`), both only inside `@if (Model.HasProfilePicture)`, no `onerror`; fields in their current order, `.form-text` help with no muted utility, no validation class on messages, the Markdown editor partials unchanged, `hr` and `div.form-actions` with Cancel left (Index on Create, Details on Edit) and the `btn-primary` commit right (Create Character, Save Changes). The dead edit-mode branches in Create are gone, so Create builds no picture URL.
- `character-form.css`: one `@layer pages` block, tokens only, no important flag: `.form-picture-current`, `.form-picture-thumb` (square, max 200px), `.class-remove` (nowrap, full width inside a max-width 767.98px media query for where the columns stack).
- Guards (none deleted; the rewrite is logged in `tests-renamed.md`): `MobileViewsTests.GetMobilePage_CharacterCreate_...` and `GetMobilePage_CharacterEdit_ReturnsSuccessAndMobileLayout` keep their names and now assert the merged view (phone 200, `mobile-layout`, `form-actions`, `character-form.css`; neither `character-form-card` nor the phone sheet). New `MarkdownEditorMarkupTests.CharacterForms_RenderBothEditorsWithHintsAndLabelsUnderBothUserAgents` (4 cases; the desktop Create fact stays). `CharactersConversionGuardTests`: Create, Edit and the partial added to `ConvertedViews` and `ViewSheetLinks`, `character-form.css` to `ConvertedSheets` and `PageOnlySheets`, the three deleted files to `RetiredFiles` and removed from `PhaseSourceFiles` (the new files added there), Create and Edit added to `CharacterPictureViews`. `UiFoundationLayerGuardTests`: `character-form.css` appended to `LayeredFiles` and `NoLiteralFiles`.
- `CharacterFormRenderTests` (26 cases): same contract under both User-Agents, the template with the index token and accessible names, required markers with the hidden word, action row shape per form, Create one row with no Remove and no picture leftovers, Edit with two classes (Classes[0] and Classes[1], Remove on the second only), Edit with no stored classes (one row at index 0, the index fix), Edit with a picture (URL, class, Re-crop with its original URL, thumbnail after the input) and without one (no picture URL, no Re-crop, no onerror).
- `legacy-collision.py` prints nothing on Create, Edit and the partial; `grep -cE "\sstyle\s*="` prints 0 on both views.

## Task 3: suites, sweep, look check, editor check

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2208 passed, 0 failed, 16 skipped, 2224 total (2175 passed before; plus 33 new cases: 26 render, 2 phone-marker, 4 editor-markup, 1 module guard).
- **Sweep** of `char-create`, `char-create-errors`, `char-edit`, `char-edit-nopic`, `crop-char-create`, `crop-char-edit-recrop` under both User-Agents at every width: 24 captures, 0 not 200. `readability-compare.py` against BEFORE with `allow.json`: 24 compared, 0 failing rows, 0 poster-glyph violations, `ok`.
- **Allowed differences** (`allow.json`, renamed accessible names, no control lost): Character Name and Level gain ` (required)`; a row's Class select is now named `Class` (was its option list) and its level input `Class level` (was the placeholder `Level`). Without the allow file the compare reported exactly those six renamed keys per row and nothing else.
- **Look check** (`lookcheck.json`): phone against `quest-form.mobile.html`: 16 properties, pass, only the two intended differences. Desktop against `quest-form.html`: the two intended differences plus `h1.color` (reference parchment, page gold `rgb(255, 193, 7)`), the approved gold form title, so the tool reports pass false for that one property only.
- **Editor keyboard check** (`editor-trap.json`, 8 records: Description and Backstory on Create and Edit under both User-Agents): Tab and Shift+Tab leave each editor, Escape keeps focus usable, the commit is reached in 2 or 3 Tab presses. All `ok`.
- I viewed the captures: desktop Edit shows the gold Cinzel title, the picture block level with the Name field with the 200px thumbnail and Re-crop under the hint, the two class rows with Remove only on the second, and one action row (Cancel left, wood Save Changes right). Phone Create shows the stacked blocks with Create Character first on screen and Cancel below; phone Edit shows a full-width Remove and a one-row Cancel plus Save Changes.
- `QuestBoard.IntegrationTests/TestHostFileWatching.cs`, `QuestBoard.Service/Controllers` and `QuestBoard.Service/ViewModels` are untouched (git diff against PLAN_START is empty).

## Deviations from Plan

### Plan interpretations

1. **[Tracer gate]** No auto flag is set and a sequential subagent cannot be resumed, so the gate was the automated re-run plus the live class-rows check under both User-Agents (as in 98-13 and 98-15).
2. **[Files]** A shared partial `_CharacterClassRows.cshtml` was added beyond the plan's list so the row markup exists once instead of twice; it is in the guard lists.
3. **[Tool]** `class-rows.mjs` creates and then removes its own clearly named probe characters; the stale probes from its first, failing run were removed by the same exact-name rule.
4. **[Test-first]** The render tests were written and run before the views changed in Task 1; the layout facts failed until Task 2, as intended.
5. **[Editor trap check]** The plan's inline python assumed a dict; `editor-trap.json` is a list of records, so the same three conditions were checked per record.

No bugs or missing-functionality fixes were needed outside the plan.

## Known Stubs

None.

## Threat Flags

None. No controller, view model, POST field name, antiforgery token or validator changed (T-98-42, T-98-43); the class names keep the `Classes[i].Class` and `Classes[i].ClassLevel` shape and `CharactersControllerIntegrationTests` stays green under both User-Agents. The module rewrites only name, id and for values on a clone of a server-rendered template (T-98-41).

## Self-Check: PASSED

- `Create.cshtml`, `Edit.cshtml`, `_CharacterClassRows.cshtml`, `character-form.js`, `character-form.css` and `CharacterFormRenderTests.cs` exist; `Create.Mobile.cshtml`, `Edit.Mobile.cshtml` and `character-form.mobile.css` are gone; commits `40f5afeb` and `ed4f6afd` exist.
- Report, sweep captures, `class-rows.json`, `lookcheck.json`, `editor-trap.json` and `allow.json` exist under `~/qb-ui-review/phase-98/after/98-17/`.
