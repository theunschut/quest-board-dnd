---
phase: 95-quests-and-quest-log-ui-conversion
plan: 10
subsystem: ui-quest-forms
tags: [quest-create, quest-edit, view-merge, date-rows, layered-css, contrast, wall-clock, tracer]
requires:
  - phase: 95-04
    provides: quest-form.js submit guard, editor hint, QuestsConversionGuardTests
  - phase: 95-07
    provides: foundation card pattern for the phone, probe and allow-file chain
provides:
  - proposed-date rows added and removed by quest-form.js through data-action, built with DOM calls
  - Quest Create on the foundation on both layouts, Cancel left and Create Quest right
  - Quest Edit merged into one view serving both layouts (Edit.Mobile.cshtml removed)
  - quest-form.css and a rewritten dm-create.mobile.css in the pages layer
  - zero text-contrast failures on every Create and Edit state, both layouts, all widths
affects: [Quest Log slice (EditRecap still links quest-edit.mobile.css), CreateFollowUp slice (shares the form scripts partial)]
tech-stack:
  added: []
  patterns: [required marker and validation message on the opaque overlay surface, help text taking the card colour on a page scope, stable row ids for labels of rows added in the browser, existing date shown through Html.WallClock with the machine value kept hidden]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/quest-form.css
  modified:
    - QuestBoard.Service/wwwroot/js/quest-form.js
    - QuestBoard.Service/wwwroot/js/site.js
    - QuestBoard.Service/Views/Quest/Create.cshtml
    - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Edit.cshtml
    - QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml
    - QuestBoard.Service/wwwroot/css/dm-create.mobile.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
key-decisions:
  - "Required markers and server validation messages sit on the opaque overlay surface: the light danger text measures 3.63:1 on the card glass at its lightest, 10.05:1 on the overlay. The marker is a small dark chip, the message a dark strip, the summary a dark panel with a danger stripe; words and the 2px field border carry the meaning, not colour"
  - "Help lines (.form-text, including the editor -help hint) take the card colour on the quest form page scope, as on Quest Details, because the framework muted tone is 4.25:1 on one glass card"
  - "Quest/Edit merged (Gate 1: yes): one Edit.cshtml, phone tweaks under .mobile-layout in quest-form.css, no media query; quest-edit.mobile.css left untouched for EditRecap"
  - "Rows added in the browser get their own input id so each label points at its input; Remove buttons carry an aria-label naming the date (Remove proposed date N), renumbered with the rows"
requirements-completed: [D-16, D-17, D-18, D-21, D-24, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8, DoC-9]
duration: ~2 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 16000
  tasks: 2
  commits: 3
---

# Phase 95 Plan 10: Quest Create and Edit on the foundation, Edit merged Summary

Both quest forms are on the foundation on both layouts with the approved Cancel-left, commit-right action row, the proposed-date rows run from `quest-form.js` with no inline code, and Quest/Edit is one view serving the phone too. Every Create and Edit state measures zero text-contrast failures.

## Merge branch taken

**Merge = yes.** Gate 1 item 8 (`~/qb-ui-review/phase-95/gate1/answers.json`): Quest/Edit yes (CreateFollowUp and Move also yes, outside this plan). `Edit.Mobile.cshtml` is deleted; `Edit.cshtml` serves both layouts (`col-lg-8` form and `col-lg-4` tips card, the phone stacks them). `quest-edit.mobile.css` is unchanged and stays for `QuestLog/EditRecap.Mobile.cshtml`. `GetMobilePage_QuestEdit_ReturnsSuccessAndMobileLayout` is rewritten to the same intent: under a phone User-Agent Edit answers 200, the page carries `mobile-layout`, the form posts to `/Quest/Edit/{id}`, and it shows `Update Quest`, `Quest Editing Tips` and `quest-form.css`. The two facts about the deleted twin (`quest-edit-card-mobile`, `quest-edit.mobile.css`) went with the twin.

## What changed

**Date rows (commit `015673ab`).** `quest-form.js` now owns the rows. One delegated click listener handles `[data-action="add-proposed-date"]` and `[data-action="remove-proposed-date"]`. A new row is built with `createElement` and property assignment (label with `for`, `input-group`, `datetime-local` input with `required` and `step="60"`, `btn btn-danger` with `fa-trash` `aria-hidden` and the word `Remove`). The field-name prefix logic (`ProposedDates` versus `Quest.ProposedDates`), the keep-at-least-one-row rule, renumbering after a removal and the pre-fill (day after the last filled date at 18:00, else today at 18:00) are unchanged. `addProposedDate` and `removeProposedDate` are deleted from `site.js`; the rest of that file is untouched. The submit guard keeps its `form[action*="/Quest/"]` scope.

**Create, desktop and phone.** `h1` in the card header, labels with `aria-hidden` required markers, help text linked by `aria-describedby`, validation spans, `Add Another Date Option` as `btn-secondary` with `fa-plus` (label unchanged), then `<hr>` and `Cancel` (`btn-secondary`, `fa-times`, to the board) left and `Create Quest` (`btn-primary`, `fa-plus`) right. The phone gains the Challenge Rating and Proposed Dates helper sentences, the full Dungeon Master Session sentence and the desktop description placeholder, verbatim. `dm-create-card-mobile` is kept as a marker class on a foundation `.modern-card`. The Quest Creation Tips card stays desktop-only. The session row on both layouts: label has no colour class (takes `--qb-fg`), crown is `aria-hidden`.

**Edit (commit `149198c5`).** Same conversion, plus: the existing-signups `.alert.alert-warning` sits directly above the Proposed Dates group inside the form card on both layouts; each existing date is `Html.WallClock(date, "date-time")` in a `form-control-plaintext` block with its hidden `yyyy-MM-ddTHH:mm` input; per-date validation span (now also on the phone); Quest Editing Tips as a second card (`h2`), finalized sentence verbatim; `Cancel` goes to Manage, `Update Quest` is `btn-primary` with `fa-save`. Campaign and finalized Razor conditions are copied unchanged.

**Sheets.** `quest-form.css` (one `@layer pages` block, tokens only): help text colour, the overlay-surface treatment for markers and messages, date-row button sizing, the saved-date block, the warning gap, and the phone rules under `.mobile-layout` for the merged Edit (gutter, session row, equal-width action buttons). `dm-create.mobile.css` rewritten into `@layer pages` with the session row as the whole tap target. Create.Mobile links both sheets.

**Conversion guard.** `Quest/Create.cshtml`, `Create.Mobile.cshtml` and `Edit.cshtml` in `ConvertedViews`; both sheets in `ConvertedSheets` and `PhaseSourceFiles`; `ViewSheetLinks` entries for the three views.

## Removed legacy rules

None. No unlayered rule in `legacy-pages.css`, `quests.css` or `mobile.css` reaches a quest form element (searched for form, label, input-group, proposed-date, tips, alert and button selectors), so there was nothing to delete or narrow. `quest-edit.mobile.css` still holds the old unlayered phone Edit rules; it stays only for EditRecap, and its header comment still names the deleted twin (left untouched as the plan requires; the Quest Log slice retires it).

## Date-row probe

Scratch probe (`dateprobe.mjs`, intercepts the POST so nothing is written), real phone and desktop User-Agents:

| Page | Add x3, remove the second new row | Result |
|------|----------------------------------|--------|
| Create | `ProposedDates[0..2]` then `[0],[1]`, labels `Proposed Date 1, 2`, Remove names renumbered | form posts both dates (`ProposedDates[0]`, `[1]`) |
| Edit (3 saved dates) | `Quest.ProposedDates[0..5]` then `[0..4]`, saved hidden values kept | form posts five dates with the `Quest.` prefix |

Identical on desktop and phone, no console or page error. Server-side validation errors (empty title, rating 25) render on both pages and both layouts with the marker, message and summary at 10.05:1.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1300 passed; integration 1775 passed, 11 skipped (pre-existing), 0 failed.
- Tracer gate: the Create task's sweep, readability compare and measured-ratio check were re-run on the committed code before the Edit task started and passed (`⚡ Tracer verified end-to-end — expanding`).
- Final sweep `~/qb-ui-review/phase-95/after/95-10/` (rows `create`, `create-errors`, `create-editor-focused`, `edit`, `edit-finalized`, `log-editrecap`; 24 records, desktop 1440/320, phone 412/390). `readability-compare` with `--zero-contrast` over the five Create and Edit rows: no regression on any of them; residual in-content contrast total on those rows is 0. The only failing rows are the four `log-editrecap` records (below).
- Measured phone ratios (both `create` and `edit`, both widths): `.form-check-label` 4.68 (was 1.77), required marker 10.05, `.form-text` 4.68. The session row is a flex row at least 44px tall with the label filling it.
- BEFORE contrast on these rows is cleared, including the editor `-help` hint carried since the editor plan (it measured 4.25 desktop, 2.99 phone).
- No new overflow at 320 desktop or 390 phone, no new small target, no new console error. The editor toolbar buttons that were 30px wide on the phone are as BEFORE.
- Allow file `after/95-10/allow.json` (the previous plan's file plus): phone `Back to Quest Board` and `Back to Quest` to `Cancel`, the shorter phone description placeholder to the desktop one, the three read-only date text inputs on Edit (now a plain-text block) as removed, and the Edit `main` landmark name (it began with the moved warning).

### Contrast carried by a sheet another plan owns

`log-editrecap` (not converted here) fails `readability-compare` on `div#recap-help`: desktop 4.25 (framework muted text on the EditRecap page's own card) and phone 2.99 (the unlayered `.form-text` rule in **`quest-edit.mobile.css`**, still linked by `EditRecap.Mobile.cshtml`). This is the editor hint added in the editor plan, and it clears when the Quest Log slice that merges and converts EditRecap retires `quest-edit.mobile.css`. Two entries (desktop, phone) were added to `contrast-exceptions.json` so Gate 2 sees it; it is not a zero-contrast row of this plan. No Create or Edit state has an exception.

### 390px abbreviation check

No label was abbreviated. `Update Quest` and `Cancel` fit side by side at 390 (the buttons share the row by content width), and the date row Remove button fits beside the date at 390 and 412. No abbreviation exception.

## Deviations from Plan

**1. [Ordering] Ran out of wave order.** 95-08 (desktop board) and 95-09 (phone board) are held while the poster text-zone redraw is decided, so their SUMMARYs do not exist; this plan depended on 95-09 only to serialise shared files. Only this plan's parts of `site.js`, `legacy-pages.css` (nothing needed), `MobileViewsTests.cs` and `QuestsConversionGuardTests.cs` were changed, so 95-08 and 95-09 can apply theirs later. `95-03-SUMMARY.md` ("Merge answers") also did not exist yet (that plan was running in parallel); the merge answer was read from `gate1/answers.json` item 8 instead.

**2. [Rule 1 - Bug] Required marker and validation messages could not reach 4.5:1 on the glass.** The probe scored the light danger text at 3.63:1 on a single card, so the plan's "foundation danger text variant" alone fails. The marker, the field messages and the summary now sit on the opaque overlay surface in `quest-form.css` (10.05:1). The empty validation spans carry no static `field-validation-error` class (the tag helper adds it only on error), so no empty chip shows.

**3. [Rule 2 - Missing critical] Row-level accessibility.** New and server-rendered rows get a label tied to the input (`for` and a unique id), saved dates on Edit use a text label (a hidden input cannot be labelled), and each Remove button has an `aria-label` naming its date, renumbered with the rows. The proposed-dates group is `role="group"` labelled by its heading.

**4. [Judgement] The session-row rule is in two sheets.** The plan puts it in `dm-create.mobile.css` for Create and (merge branch) in `quest-form.css` under `.mobile-layout` for Edit. Both exist, scoped to different wrappers, so there is some duplicated CSS rather than a shared rule that would have left `dm-create.mobile.css` empty. The shell already gives a 44px row; these rules make the label the whole flex row.

**5. [Judgement] Tips-card heading is the display step.** The tips card header is an `h2` inside `.modern-card-header`, so it takes the Cinzel display size like the Quest Summary card on Details; it competes a little with the page title on desktop. Kept to match the plan and the Details sidebar.

**6. [Judgement] No section headings.** The Direction A mockup groups fields under "The Quest", "The Table", "When" headings and shows a state-preview box. The plan and the copy contract list neither, so no new words were added.

**7. [Rule 3 - Blocking] Stale comment.** `_QuestFormScripts.cshtml` said the date functions live in `site.js`; updated (third commit `2a648c11`). Not in the plan's file list.

**8. Tracer gate.** Treated as autonomous (no human present): the tracer verify was re-run end to end after its commit and passed before the Edit task.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. Rows are built with `createElement` and property assignment, no markup string and no handler attribute (T-95-25). Field names follow the unchanged prefix logic and the server validation is untouched. The Campaign and finalized Razor conditions are copied unchanged; `QuestCampaignUiParityTests` and `QuestFinalizedEditTests` pass unchanged (T-95-26).

## Notes for later slices

- `quest-edit.mobile.css` still contains the old unlayered Edit-twin rules and a header comment naming the deleted twin; the Quest Log slice that merges EditRecap should delete the file.
- `CreateFollowUp` shares `_QuestFormScripts` and the module but has its own markup (no `data-action` rows yet); when it converts it can reuse `quest-form.css`, `.quest-form-actions` and the `.quest-form-wrap` phone gutter.
- The Create phone view still wraps its card in its own `container-fluid` on top of the layout's, so its gutter is 8px wider than the merged Edit on the phone; `.quest-form-wrap` gives Edit the matching gutter.
- Working-tree app left running on `http://localhost:8000`, rebuilt from the last commit. Port 8001 untouched.

## Self-Check: PASSED

Verified: `quest-form.css` exists, `Edit.Mobile.cshtml` is gone; commits `015673ab`, `149198c5` and `2a648c11` exist; full `dotnet test` green; `readability-compare` with `--zero-contrast` shows no Create or Edit failure; the measured phone ratios for `create` and `edit` print `ok`.
