---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 08
subsystem: form-contract-and-category-edit-merge
tags: [ui-conversion, form-contract, components-layer, merge, guard-tests]
requires: [98-07]
provides:
  - ".form-page and .form-actions rules once, in the components layer of components.css"
  - "ContactCategoryManagement/Edit as one view for both layouts (phone twin deleted)"
  - "CharactersConversionGuardTests lists ConvertedViews, ConvertedSheets, ViewSheetLinks, RetiredFiles and their facts"
  - "legacy-collision.py scratch tool"
affects: [98-09, 98-10, 98-11, 98-13, 98-14, 98-15, 98-16, 98-17, 98-18, 98-19, 98-20, 98-21, 98-22]
tech-stack:
  added: []
  patterns: [one view serving both User-Agents behind the same gates; guard lists that later slices append to; a collision tool that lists still-global unlayered rules a converted view could match]
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml
    - QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/ContactCategoryManagement/Edit.Mobile.cshtml
decisions:
  - "Form contract ported from the approved form-contract mockup unchanged: help text in --qb-fg, required marker and field messages on the overlay chip, summary with a 4px danger stripe, one-row action row with the phone shape one-row (exit natural width, commit filling) and an opt-in .form-actions--stack modifier for Characters Create and DM EditProfile"
  - "Form title stays gold by the foundation rule (Gate 1 item 6): no parchment modifier added"
  - "Rename button is btn-primary (was btn-warning); exit label Cancel (Gate 1 item 14); h1 for the page title"
  - "Promotion into the Quest and Events forms not made: Gate 1 item 13 is no-promote, so those files are untouched"
metrics:
  tasks: 3
  commits: 1
status: partial
actuals:
  tokens: 4200
  tasks: 2
  commits: 1
---

# Phase 98 Plan 08: Shared form contract and the category rename merge Summary

The form contract now lives once in `components.css` and the category rename page is a single view for both layouts, proven by a phone and a desktop User-Agent test and a retired-file guard. Status is partial: both full test suites are green, but the live sweep and look check of Task 3 could not run because SQL Server at localhost:1433 is down (see Blocker).

## What changed

**Task 1 (tracer), commit `8c5c5b47`.**
- `components.css`, inside the single components layer, gains `.form-page` (help text takes the card colour; the required marker, field validation messages and the validation summary sit on the opaque overlay chip; the summary has its 4px danger stripe) and `.form-actions` (flex row, exit left, commit right; on the phone one row with the exit at natural width and the commit filling; `.form-actions--stack` is the column-reverse fallback the operator approved only for Characters Create and DM EditProfile). No colour literal, no important flag, every value a token. `UiFoundationLayerGuardTests` stays green.
- `ContactCategoryManagement/Edit.cshtml` is the one view: `form-page` wrapper (the old contacts-page wrapper is gone, so the still-global contacts sheet no longer restyles it), `h1` Rename Category with a decorative icon, the Category Name field with the required marker and a visually hidden ` (required)`, the field message through the validation tag helper, `hr`, then `form-actions` with Cancel (`btn-secondary`) at the left and Rename (`btn-primary`) at the right. No inline style, script or handler. Form posts, antiforgery token and validation are unchanged and the controller is untouched.
- `Edit.Mobile.cshtml` is deleted. `contacts.mobile.css` is not changed: every rule it holds is shared with the contacts Index, Details and Manage phone views, and none was used only by the deleted view (checked class by class), so nothing is removed from it.
- Tests written first and seen failing: `Edit_Get_DungeonMaster_RendersTheSameFormUnderBothUserAgents` (a theory over the phone and desktop User-Agent: 200, `mobile-layout` present only on the phone, same h1, field, form-actions row, Rename and Cancel). `CharactersConversionGuardTests` gains the lists `ConvertedViews` (the merged Edit first), `ConvertedSheets` (empty, grows later), `ViewSheetLinks` (Edit with the empty set: it links no sheet) and `RetiredFiles` (the phone Edit), and the facts `ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `ConvertedViews_LinkTheirSheets` (also pins that every converted view has an entry and links exactly its listed sheets), `ConvertedSheets_AreNamedInThePagesLayer` and `RetiredFiles_AreGone`. The planning-reference scan covers `components.css`, the merged Edit and the controller test file.
- `legacy-collision.py` written (scratch, `~/qb-ui-review/phase-98/tools/`): reads a view and its partials, the sheets both layouts link after `shell.css` that sit outside an `@layer` block, and prints each class with the sheet and selector whose every class the view renders (and whose ids it carries). On the old Edit view it reported the contacts-page rule; on the merged view it prints nothing.
- Tracer gate: this sequential subagent cannot be resumed and its orchestrator owns the interactive checkpoint, so the gate was the automated re-verification of the tracer's verify (the targeted filter, 143 tests passed; then both full suites after Task 2).

**Task 2: promotion not approved (default).** `gate1/answers.json` has `formPromotion.promote: false` and item 13 `no-promote` from the operator. Nothing was changed: `git diff` of `Views/Quest`, `Views/Events`, `quest-form.css` and `event-forms.css` against the plan start is empty. The shared form rules serve this area's forms only. `style-compare.mjs` was therefore not written (it exists only to prove a promotion).

**Task 3: partial.**
- Done: `PhaseSourceFiles` updated; both full suites run, see Test results.
- Not done, blocked: sweep of `cat-edit` and the six `form-*` rows under both User-Agents into `after/98-08/`, `allow.json`, `readability-compare.py` against BEFORE, and `lookcheck.mjs` on the live `cat-edit` page. `after/98-08/PLAN_START` was written (the plan-start commit `5a9dec80`).

## Test results

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2028 passed, 0 failed, 16 skipped, 2044 total (2022 before; plus the 2 theory cases and the 4 new guard facts).
- `TestHostFileWatching.cs` is untouched.

## Blocker

The sweep needs the running app against SQL Server. `localhost:1433` refuses connections: the `mssql-dev` container exited (status `Exited (137) 8 hours ago`) and is not running. Starting or provisioning the database is not allowed for this work, so it was left alone. The 8310 working-tree app failed to start on its database migration with the SQL connection error, and the 8311 baseline was already down. No process of this plan is left running. To finish Task 3 once the database is back: `node tools/run-app.mjs /mnt/Data/repos/quest-board-dnd 8310 --restart`, make sure 8311 is up, then sweep `cat-edit,form-quest-create,form-quest-create-errors,form-quest-edit,form-quest-followup,form-event-create,form-event-edit`, write `allow.json` (category Edit h1, Rename button role and the action row; nothing for the form rows), run `readability-compare.py` and `lookcheck.mjs` on `cat-edit` (desktop against `quest-form.html`, phone against `quest-form.mobile.html`, p98dm).

## Gate 1 answers applied

`formActions.phone` one-row (stack only as an opt-in modifier for the later two pages); `formPromotion.promote` false; `lookCheck.formTitle` gold; `exitLabel.label` Cancel; additional a3 label `Rename`.

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan interpretations

1. `contacts.mobile.css` is not edited: no rule was used solely by the deleted view, so the files-modified entry is a no-op. It was removed from `PhaseSourceFiles` accordingly.
2. The validation message span uses the tag helper with no class, as the Quest forms do, so the empty valid span draws no chip. The old `text-danger` class on it is gone.
3. `ConvertedSheets_AreNamedInThePagesLayer` was added beside the three named facts so the empty `ConvertedSheets` list starts being checked when the first sheet is appended.
4. `Cancel` keeps its `Url.Action` href; the page's header icon became decorative (`aria-hidden`).

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or schema; the controller is untouched. T-98-20: the form keeps `asp-action`, the antiforgery token and its validation, and the controller's existing facts stay green. T-98-21: one view serves both User-Agents behind the same controller gates; the new theory runs as the Dungeon Master tier. T-98-22: not triggered (no promotion).

## Self-Check: PASSED (for the work done)

- `Edit.cshtml` exists; `Edit.Mobile.cshtml` is gone; commit `8c5c5b47` exists.
- `.form-page` and `.form-actions` present in `components.css`; `grep -c contacts-page` on the merged Edit prints 0 and `<h1` prints 1.
- `legacy-collision.py` on the merged Edit prints nothing.
- Status is partial: the sweep and look-check evidence is not yet gathered.
