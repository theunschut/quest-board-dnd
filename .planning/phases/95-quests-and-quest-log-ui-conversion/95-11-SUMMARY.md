---
phase: 95-quests-and-quest-log-ui-conversion
plan: 11
subsystem: ui-quest-forms
tags: [quest-followup, view-merge, date-rows, contrast, layered-css]
requires:
  - phase: 95-10
    provides: quest-form.js date-row actions, quest-form.css, quest-form-actions and quest-form-wrap classes
  - phase: 95-03
    provides: Gate 1 merge answers (Quest/CreateFollowUp = yes)
provides:
  - Quest CreateFollowUp merged into one view serving both layouts (CreateFollowUp.Mobile.cshtml and quest-followup.mobile.css removed)
  - follow-up date rows run by quest-form.js through data-action, no inline script left in any quest form
  - Cancel left and Save Follow-Up Quest right after an hr, Remove as filled btn-danger with fa-trash and the word, Add Date as btn-secondary
  - zero text-contrast failures on every follow-up state, both layouts, all widths
affects: [Quest Log slice, Manage slice (still links to this page)]
tech-stack:
  added: []
  patterns: [one view for both layouts using the shared quest-form sheet, server-rendered first date row so the group is never empty]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
    - QuestBoard.Service/wwwroot/css/quest-form.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/quest-followup.mobile.css
key-decisions:
  - "Merge = yes (Gate 1 item 8): one CreateFollowUp.cshtml, the phone gets the form gutter and equal-width action row from the existing quest-form.css rules under .mobile-layout, no media query"
  - "The first date row is rendered on the server (blank if the original quest brought no date) instead of an inline script adding it on load"
  - "The phone Cancel button no longer squeezes: under .mobile-layout the secondary action keeps its content width and the longer commit label wraps"
requirements-completed: [D-16, D-17, D-21, D-24, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8]
duration: ~1 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 9000
  tasks: 2
  commits: 4
---

# Phase 95 Plan 11: Quest CreateFollowUp on the foundation, merged Summary

The follow-up form is one view on the foundation that serves the phone as well, its proposed-date rows run from `quest-form.js` with no inline code, and every follow-up state measures zero text-contrast failures.

## Merge branch taken

**Merge = yes.** Gate 1 item 8 (`gate1/answers.json`) and the "Merge answers" table in `95-03-SUMMARY.md` both read `Quest/CreateFollowUp: yes`. `CreateFollowUp.Mobile.cshtml` and `quest-followup.mobile.css` are deleted (its only consumer was the twin). `CreateFollowUp.cshtml` serves both layouts: `col-lg-8` form card and `col-lg-4` Pre-Approved Players card, which stack on the phone. `GetMobilePage_QuestCreateFollowUp_ReturnsSuccessAndMobileLayout` is rewritten to the same intent: under a phone User-Agent the page answers 200, carries `mobile-layout`, posts to `/Quest/CreateFollowUp/{id}`, shows `Save Follow-Up Quest`, `Pre-Approved Players`, the pre-approved sentence and links `quest-form.css`. The two facts about the deleted twin (`quest-followup-card-mobile`, `quest-followup.mobile.css`) went with it; no other fact was removed.

## What changed

**View (commit `5bb4c7ff`).** `h1` in the card header; `asp-validation-summary` in the card body; required markers `aria-hidden`; validation spans without the colour class (the sheet from the previous plan puts markers and messages on the overlay surface); Challenge Rating help linked by `aria-describedby`. Date rows are `.proposed-date-item` rows, each with a visible `Proposed Date N` label tied to its `datetime-local` input by id, a `btn btn-danger` Remove (`data-action="remove-proposed-date"`, `fa-trash` `aria-hidden`, the word `Remove`, `aria-label` naming the date). `Add Date` is `btn-secondary` with `fa-plus` and `data-action="add-proposed-date"`; the Proposed Dates help sentence is the Create one, verbatim. `<hr>`, then `Cancel` (`btn-secondary`, `fa-times`, to Manage of `Model.OriginalQuestId`) left and `Save Follow-Up Quest` (`btn-primary`, `fa-scroll`) right. The Pre-Approved Players card is a second `.modern-card` with an `h2`, its sentences verbatim, and each listed player carries the check icon plus a visually hidden "Pre-approved:" word. The whole inline script is gone; the module's actions add rows with the same `ProposedDates[n]` names and the same pre-fill. Field names, the hidden `OriginalQuestId` and `DungeonMasterId`, and the form action are unchanged.

**Sheet (commit `24c4fc2f`).** `quest-form.css` gained one phone rule: the secondary action keeps its content width so the icon stays beside the word, and the longer `Save Follow-Up Quest` label wraps instead. It applies to Create and Edit as well (their labels already fit, so nothing there moves).

**Conversion guard (commit `9f3e83b6`).** `Quest/CreateFollowUp.cshtml` is in `ConvertedViews` and `ViewSheetLinks` (`quest-form.css`). `ConvertedSheets` needed no entry: the no-merge sheet is gone and `quest-form.css` is already listed.

## Removed legacy rules

None. No unlayered rule in `legacy-pages.css`, `quests.css` or `mobile.css` reaches a follow-up form element (searched for proposed-date, follow-up, form-control, alert and button selectors), so `legacy-pages.css` was not touched. `quest-followup.mobile.css` was deleted whole, including its `!important` and colour literals.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1300 passed; integration 1775 passed, 11 skipped (pre-existing), 0 failed. Task 1 filter (MobileViewsTests, QuestManageDmControlsTests, MarkdownEditorMarkupTests, UiFoundationViewLibraryGuardTests): 66 passed.
- Sweep `~/qb-ui-review/phase-95/after/95-11/` (rows `followup`, `followup-errors`, `followup-editor-focused`, plus `create` and `edit` re-swept after the shared sheet edit; desktop 1440/320, phone 412/390). `readability-compare` with `--zero-contrast` over the three follow-up rows: no problems, residual in-content contrast 0. The same compare over Create, Create-errors, Create-editor-focused, Edit and Edit-finalized (fresh sweep `after/95-11-forms`) also passes, so the shared sheet edit regressed nothing.
- Measured phone ratios on `followup` (both widths): required marker 10.05, `.form-text` 4.68; the required-marker selector matched 4 elements, so the check printed `ok`. Validation messages and the summary read at 10.05 on the overlay surface.
- No overflow at 320 desktop or 390 phone, no console or page error, no failed request, on any follow-up state. With validation errors (title cleared, rating 25, native validation off) and with the editor focused, nothing new fails.
- Date-row probe (intercepts the POST, real desktop and phone User-Agents): start with 1 row; add three; remove the second-from-last; names renumber `ProposedDates[0..2]`, labels `Proposed Date 1..3`, Remove names renumbered; the form posts exactly those three date fields. Identical on both layouts.
- Real save: fixtures step `finalized` in `tools/fixtures.mjs` now creates a second finalized source on the review board (`quest.followUpSource2` = 992030, so the `followup` row's source keeps having no follow-up) and saves its follow-up through the form: Manage, click `Create Follow-Up Quest`, new title, add, add, remove the second (asserts 2 rows), fill both dates, submit. Recorded `quest.followUp2` = 992031, `created: true`. Its Manage page lists both dates and "Continues from: The Ember Gate Assize".
- Allow file `after/95-11/allow.json` (the previous plan's file plus, for the three follow-up rows): `Back to Quest` to `Cancel`; the date input's key gaining its id; the previously nameless icon Remove button now named `Remove`.
- No contrast exception was added: none of the follow-up states needs one.
- 390px abbreviation check: no label abbreviated. `Cancel` and `Save Follow-Up Quest` share the row (the commit label wraps to two lines at 390); Remove fits beside the date at 390 and 412.

## Deviations from Plan

**1. [Ordering] Ran out of wave order.** 95-08 and 95-09 (the boards) are not done; this plan only touched its own parts of the shared files (`quest-form.css`, `MobileViewsTests.cs`, `QuestsConversionGuardTests.cs`). `legacy-pages.css` and `quest-form.js` needed no change. `95-03-SUMMARY.md` appeared while this plan was running, and confirmed the merge answer already read from `answers.json`.

**2. [Rule 3 - Blocking] Scratch tooling extended.** `followup-errors` clears the title and sets rating 25 before `submitEmpty`, because the form arrives pre-filled and an empty submit would raise no error. The save needed a source quest with no follow-up, so `fixtures.mjs` got a second source rather than spending `quest.finalized` (which would have turned the `followup` row into a redirect). These writes are on the Audit Board only, through the app's own forms (the plan's required real save).

**3. [Rule 1 - Bug] Phone Cancel squeezed.** On the first render the phone Cancel button stacked its icon above its word because the longer commit label took the room; fixed with the one `.mobile-layout` rule above.

**4. [Judgement] Behaviour change to be aware of.** The old inline script let a DM remove every date row (the guard alert then fired on submit); the module keeps at least one row, as on Create and Edit. The first row is server-rendered and `required`. The submit guard still stops a form with no filled date.

**5. [Judgement] One new helper sentence.** The follow-up gained the Proposed Dates help line (the Create wording, verbatim) to give the dates group its `aria-describedby`; the plan asks for help text on the groups.

**6. [Judgement] Submit label.** The module's submit guard still swaps the button text to "Creating Quest..." while posting; this was already the behaviour on this page and is left as is.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. Rows are built by the shared module with `createElement` and property assignment, no markup string and no handler attribute (T-95-27). The view keeps the hidden original-quest id and the server-side authorisation is unchanged (T-95-28).

## Notes for later slices

- `quest-edit.mobile.css` (still linked by `EditRecap.Mobile.cshtml`) is unchanged by this plan.
- Working-tree app left running on `http://localhost:8000`, restarted on the build with this view. Port 8001 untouched.

## Self-Check: PASSED

Verified: `CreateFollowUp.cshtml` exists; `CreateFollowUp.Mobile.cshtml` and `quest-followup.mobile.css` are gone; commits `5bb4c7ff`, `24c4fc2f` and `9f3e83b6` exist; full `dotnet test` green; `readability-compare` with `--zero-contrast` passes on the follow-up and form rows; measured phone required-marker ratio prints `ok`; `fixtures.json` has `quest.followUp2` with `created: true`.
