---
phase: 100-admin-and-platform-ui-conversion
plan: 17
subsystem: ui
tags: [razor, platform, group-create, group-edit, form-contract, switch-rows, layered-css, guard-tests, mobile-twins]
requires:
  - phase: 100-12
    provides: Gate 1 answers (One-Shot wording, button roles, required markers, accessible names)
  - phase: 100-13
    provides: AdminPlatformDisplay board-type options, the area guard lists, .control-reason
  - phase: 100-15
    provides: the form contract (form-page, required markers, form-actions, validation on the foundation treatment)
  - phase: 100-16
    provides: platform-pages.css and platform-pages.mobile.css
provides:
  - Platform Group/Create on both layouts: one h1 in the card header, .form-page, required markers on both fields, board-type select from the shared mapping (values unchanged, placeholder kept), help as .form-text, one btn-primary commit
  - Platform Group/Edit on both layouts: one h1, an h2 Pages heading, one switch row per page with the label covering the row, a locked page's sentence as a .control-reason, page-level refusals as role=alert .refusal-line above the form, mapped board type in the disabled select
  - platform-pages sheets grown with .form-section-title, the refusal-line surface, and the .page-switch row rules (32px desktop, 44px phone)
affects: [100-18, 100-19]
tech-stack:
  added: []
  patterns: [a switch row is form-check form-switch page-switch with the label as a block that fills the row; the pill width is restored in the page sheet because the shared checkbox rule squares every .form-check-input; a refusal above a form sits on the opaque overlay surface with a stripe, because the danger text only holds 4.5:1 there]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/platform-pages.css
    - QuestBoard.Service/wwwroot/css/platform-pages.mobile.css
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
key-decisions:
  - "Gate 1 item 6a yes: the Create select reads One-Shot and Campaign from AdminPlatformDisplay.BoardTypeOptions(); the Edit select shows the same words"
  - "The switch label is a block that fills the row (not a wrapping label element), so the existing facts that read the label text and the switch input by id keep their meaning"
  - "Checked switches take the gold the reserved list allows; the pill width (2em) is set in the page sheet because the foundation checkbox rule had been squaring the switches into radio-looking circles"
  - "A locked row stays a natively disabled switch (so it cannot toggle and posts nothing); its label is kept at full strength and the lock line names the reason"
  - "Both phone action rows use form-actions--stack: Create Group and Save Changes wrap beside Back to Groups at 390px"
  - "Group/Delete's #board-delete-help contrast failure is NOT fixed here: Delete links only the Phase 102 delete sheets, not platform-pages.css, which this plan owns"
requirements-completed: [D-15, D-16, D-19, D-21, DoC-1, DoC-4, DoC-8, DoC-10]
duration: ~1h
completed: 2026-11-18
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 100 Plan 17: Platform Group create and edit Summary

**The Platform board forms read as the rest of the converted forms: one title in the card header, required markers, the board type spelled One-Shot or Campaign from the shared mapping, and an Edit page whose nine page switches are real pills in readable 32px (desktop) and 44px (phone) rows, with locked rows explaining themselves and refusals shown above the form.**

## What was built
- **Create (both layouts):** card header h1 "Create Group", `form-page`, Group Name and Board Type with the required marker and a visually-hidden "(required)", `autocomplete="off"` on the name, the board-type select built with `asp-items="AdminPlatformDisplay.BoardTypeOptions()"` plus the kept `-- Select Board Type --` placeholder, the "This cannot be changed after creation." help as a `.form-text` linked by `aria-describedby`, validation messages on the foundation treatment, and `.form-actions` with Back to Groups left and the single `btn-primary` (was `btn-success`) Create Group right. The phone row stacks.
- **Edit (both layouts):** h1 "Edit Group"; the same name field; the disabled board-type select showing the mapped word; a `role="group"` section with an `h2` "Pages" (was `h5` on desktop and `h6` on the phone), the hint as `.form-text` (was `small.text-muted`), and nine `form-check form-switch page-switch` rows; a locked row's existing sentence as `<p class="control-reason">` with a lock icon, tied to its switch by `aria-describedby`; the Home page select with its hint; a stale-form or page-rule refusal as `<div role="alert" class="refusal-line">` above the form; Back to Groups left and `btn-primary` Save Changes right (was `btn-warning`).
- **Posted names unchanged:** `Id`, `RenderedDisabledPages`, `RenderedHomePage`, `Name`, `Pages[i].Page`, `Pages[i].Enabled` (value and hidden false), `HomePage`. No controller or view model change.
- **Sheets:** `platform-pages.css` gained `.form-section-title`, the `.modern-card-body > .refusal-line` surface, and `.page-switch` (row and label 32px, pill width, gold checked fill, full-strength disabled label, the reason in the card colour); `platform-pages.mobile.css` raises the switch row and label to `--qb-hit-min`.
- **Guards and facts:** the Create and Edit pairs moved from PendingViews to ConvertedViews, got ViewSheetLinks entries and joined PhaseSourceFiles; 26 new render cases (10 Create, 16 Edit) in AdminPlatformRenderTests on both layouts.

## Task commits
1. Task 1 (tracer, Create end to end): `056b560c`
2. Task 2 (Edit, switches, refusal, guard lists): `0c8e5d18`
3. Task 3 (sweep, summary, docs): the docs commit following this file

The tracer gate ran in autonomous mode: its verify (render facts plus GroupManagementIntegrationTests, 44 passed) was re-run after the phone stack change and passed before the Edit slice started.

## Verification
- Build 0 errors. Unit 1636 passed. Integration 2987 passed, 19 skipped (the same 19 reproduction facts as before; 26 new cases).
- Sweep `group-create`, `group-edit` plus afterOnly `group-create-invalid`, `group-edit-campaign`, `group-edit-refused` under both User-Agents (20 captures, 0 non-200): readability-compare against BEFORE 8 compared, 0 failing. Contrast failures 0, overflow none at 320 (desktop) and 390/412 (phone), small targets 0, missing focus rings 0, console and page errors 0, no action or assert failure on any row.
- `group-edit-refused` switches every page off and submits: the server refused it, two `.refusal-line` alerts show above the form, and the next load of Group/Edit shows every page still on (nothing was written to the P100 Audit One-Shot fixture).
- Screenshots and the report: `~/qb-ui-review/phase-100/after/100-17/`. The `allow.json` there holds only accessible-name renames (the visible "* (required)" text is now part of each field's name, and the main region's name) per `<id>@<ua>`.

## Deviations from Plan

### Auto-fixed Issues
**1. [Rule 1 - Bug] Switches rendered as radio-looking circles**
- **Found during:** Task 2 sweep screenshot review
- **Issue:** the shared `.form-check-input { width: 1.25em }` rule overrides Bootstrap's 2em switch width, so every page switch had been a round dot (same in the frozen baseline), and the gold checked state made it look like a radio.
- **Fix:** `.page-switch .form-check-input { width: 2em }` in `platform-pages.css`. The shared rule is untouched.
- **Files modified:** `QuestBoard.Service/wwwroot/css/platform-pages.css`
- **Commit:** `0c8e5d18`

**2. [Rule 2 - Missing critical] Refusal line needed its own surface**
- **Issue:** `.refusal-line` danger text falls to about 3.6:1 on the card's glass.
- **Fix:** `.modern-card-body > .refusal-line` carries the overlay surface and a stripe, like the validation summary.
- **Commit:** `0c8e5d18`

**3. [Rule 2 - Missing critical] Disabled-row label and reason were under 4.5:1**
- **Issue:** Bootstrap halves the label of a disabled switch, and `.control-reason`'s muted tone measures 4.25:1 on glass.
- **Fix:** `.page-switch .form-check-input:disabled ~ .form-check-label { opacity: 1 }` and `.page-switch .control-reason { color: inherit }`.
- **Commit:** `0c8e5d18`

### Plan adjustments (not bugs)
- **Label as a block, not a wrapping label:** the plan says the whole row is the label. The label element stays a sibling after the input and is a block filling the row at the required height, because three older facts read `form-check-label ... >Label</label>` and the switch input by id. The clickable area is the whole row either way.
- **Gold checked state:** `accent-color` has no effect on a Bootstrap switch (it is drawn with a background), so the checked fill is set as `background-color: var(--qb-accent)`; `accent-color` is also set for native fallback.
- **Extra sweep rows:** `group-edit-campaign` (locked rows) and `group-edit-refused` (the refusal line) added to the manifest next to the planned `group-create-invalid`.
- **Phone action rows stack** (`form-actions--stack`) on both phone forms; the commit label wrapped at 390px otherwise.
- **No existing test was rewritten:** `tests-renamed.md` has no 100-17 entry.

## Open Issues
- **Group/Delete `#board-delete-help` (4.25:1) left as is.** Delete links `platform-group-delete.css` and `platform-group-delete.mobile.css` only; neither is a sheet this plan owns, and `platform-pages.css` is not linked by Delete. A rule there (or linking `platform-pages.css` from Delete) is for 100-19 or the cleanup plan.
- `platform-group.mobile.css` is still linked by Group Members (phone) and used by the LinkedBoards phone page; it stays in PendingSheets. `BoardPageStaticGuardTests` still names the Edit views and the sheet.

## Known Stubs
None.

## Threat Flags
None. T-100-58 is covered by the render-then-post facts (create and rename) plus `BoardPagePlatformTests` and `GroupManagementIntegrationTests`; T-100-59 by `asp-action` forms that keep the token (the Create fact fetches and posts the issued token) and the post-form scanner over the converted views.

## Self-Check: PASSED
- Created and modified files exist; commits `056b560c` and `0c8e5d18` are in `git log`.
- Both suites green: unit 1636, integration 2987 passed + 19 skipped.
