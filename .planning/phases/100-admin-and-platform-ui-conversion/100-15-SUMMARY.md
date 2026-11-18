---
phase: 100-admin-and-platform-ui-conversion
plan: 15
subsystem: ui
tags: [razor, forms, form-page, password-hint, layered-css, guard-tests, admin-forms, mobile-twins]
requires:
  - phase: 100-12
    provides: Gate 1 answers (one title, password hint, Reset Password cancel label)
  - phase: 100-13
    provides: AdminPlatformDisplay mapping (GroupRoleOptions), admin-pages sheets, the area guard lists
  - phase: 100-14
    provides: Admin/Users converted, the guard lists as they stood
provides:
  - Admin CreateUser, EditUser and ResetPassword on both layouts per the approved drawings
  - Password Management section on the phone EditUser, with a filled Reset Password link on both layouts
  - ResetPassword with the person named in the title, new-password inputs, the rule hint and an 8-character length rule
  - admin-form.mobile.css retired; the last phone form sheet of Admin is gone
affects: [100-16, 100-17, 100-18, 100-19]
tech-stack:
  added: []
  patterns: [form page = card header with the one h1, then a form.form-page with .form-actions; required marker plus visually hidden " (required)" built around Html.DisplayNameFor so the label words stay the model's; field message spans carry an id referenced by the input's aria-describedby]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Admin/CreateUser.cshtml
    - QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml
    - QuestBoard.Service/Views/Admin/EditUser.cshtml
    - QuestBoard.Service/Views/Admin/EditUser.Mobile.cshtml
    - QuestBoard.Service/Views/Admin/ResetPassword.cshtml
    - QuestBoard.Service/Views/Admin/ResetPassword.Mobile.cshtml
    - QuestBoard.Service/ViewModels/AdminViewModels/ResetPasswordViewModel.cs
    - QuestBoard.Service/wwwroot/css/admin-pages.css
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/admin-form.mobile.css
key-decisions:
  - "Gate 1 item 2 yes: one h1 in the card header on Create and Edit User; the bare board title above the card is dropped"
  - "Gate 1 item 9 yes: ResetPasswordViewModel requires 8 characters (was 6) and the hint reads 'At least 8 characters, with an upper-case letter, a lower-case letter and a digit.', linked to the input with aria-describedby; the server's Identity policy is unchanged and stays authoritative"
  - "Gate 1 item 19 reset: only the Reset Password cancel button is renamed, to 'Back to Users'; no other Cancel label changed"
  - "The Email field of EditUser carries no required marker: its view model does not require it and a person without an address is a supported state. The approved drawing marked it; the marker would have told the Admin something untrue"
  - "The phone Reset Password uses the shared stacked action row: its commit label did not fit beside the exit on one line at phone widths"
  - "No page rule was needed for the long title: the card header already wraps any title with overflow-wrap: anywhere"
patterns-established:
  - "A field message span takes an id (<Field>-error) that the input's aria-describedby names; a help line takes <Field>-hint and goes first in the describedby list"
requirements-completed: [D-15, D-16, D-19, D-21, DoC-1, DoC-4, DoC-8, DoC-10]
duration: ~1h
completed: 2026-11-18
status: complete
actuals:
  tokens: 19000
  tasks: 3
  commits: 3
---

# Phase 100 Plan 15: Admin forms Summary

**CreateUser, EditUser and ResetPassword now read as the converted forms elsewhere do on both layouts: one page title in the card header, the form on the shared form page with the exit left and the single commit right, Password Management on the phone, the person named in the Reset Password title, password-manager-friendly password inputs with the rule hint, and the last Admin phone form sheet retired.**

## Performance

- **Tasks:** 3 (tracer, forms and guards, sweep and docs); 3 code commits (one a small follow-up fix)
- **Files:** 11 modified, 1 deleted

## Accomplishments

- EditUser (tracer): the bare board `h1` above the card is gone; the card header carries the one `h1`. Both layouts gain an `h2` Password Management, the existing sentence as form text and Reset Password as a filled `btn-danger` link (the area's only outline button is gone). Name and Email left out of autofill.
- CreateUser: one title, required markers on Email, Display Name and Group Role, Email and Name with autocomplete off, the role select built from `AdminPlatformDisplay.GroupRoleOptions()` (words Admin, Dungeon Master, Player; option values stay the enum numbers the form always posted).
- ResetPassword: `h1` "Reset Password for {name}" on both layouts, both inputs `autocomplete="new-password"` and paste-friendly, the hint under the new-password input, the existing warning alert verbatim, "Back to Users" as the exit and `Reset Password` as `btn-primary`.
- Validation markup lost its `text-danger` utilities; the foundation treatment styles the summary and field messages.
- `admin-form.mobile.css` deleted in the commit that converted its last consumer; its guard entry moved from the pending list to the retired list.

## Task Commits

1. **Task 1: EditUser on both layouts (tracer)** - `232b9d80` (feat)
2. **Task 2: CreateUser and ResetPassword, item 9 rule, phone sheet retired** - `130fdddc` (feat)
3. **Follow-up: phone Reset Password stacks its commit under the exit** - `6a829dbe` (fix)

## Gate 1 outcomes

- **Item 9 (password hint and rule): yes.** Built exactly as answered: view model minimum length 6 to 8, the hint sentence above, no other validation change. The Identity policy (8 characters, upper, lower, digit) already refuses anything the hint rules out; the client rule and hint now say the same thing. No residual remains for this item.
- **Item 19 (cancel labels): reset.** Only the Reset Password cancel is renamed to "Back to Users". The Create New User modal keeps "Cancel" (100-19).
- **Item 2 (one title): ok.** Applied to Create and Edit User.
- **Item 12 (required markers) and item 13 (accessible names):** applied, with the one exception recorded below.

## Tracer gate

The EditUser render facts were written first and failed (8 of 8), then went green with the views; the gate was re-run before the expansion task and passed (`AdminPlatformRenderTests`, `AdminAccountReachTests`, `AdminControllerIntegrationTests`, the guard, the phone tests: 135 passing at that point).

## Files Created/Modified

See `key-files`. The six views and the sheet carry no planning references; `PhaseSourceFiles` lists every file edited here.

## Verification

- `dotnet build`: 0 errors.
- Unit tests: 1636 passed.
- Integration tests: 2943 passed, 19 skipped (the same 19 reproduction and open-gap facts as before); 22 new cases in `AdminPlatformRenderTests` (EditUser: title and form contract, Password Management, markers and autofill, invalid post; CreateUser: title and contract, role select from the mapping, invalid post; ResetPassword: title and inputs, hint, seven-character refusal, mismatch refusal).
- Sweep against BEFORE (`~/qb-ui-review/phase-100/after/100-15/`): the six form rows under both User-Agents and every width: 24 compared, 0 failing. Contrast failures present before on the validation and EditUser rows are now 0; no overflow, console error, small target or missing focus ring. The allow file holds only the approved renames (accessible names gaining the required marker text, Cancel to Back to Users, the page region's name gaining the title).
- Extra after-only rows: `admin-resetpassword-short` (seven characters, validation message beside the hint) and `admin-resetpassword-longname` (a 70-character name wraps in the title at 320, 390 and 412 with no horizontal scroll).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Email on EditUser left without a required marker**
- **Found during:** Task 1
- **Issue:** the approved drawing marks Email required on Edit User, but `EditUserViewModel.Email` is optional (a person without an address is a supported state, shown as "No email provided" on Users). A marker would mislead the Admin.
- **Fix:** no marker on that field; Name keeps its marker. The view model is untouched as the plan requires.
- **Files modified:** `QuestBoard.Service/Views/Admin/EditUser.cshtml`, `EditUser.Mobile.cshtml`
- **Commit:** 232b9d80

**2. [Rule 1 - Bug] Phone Reset Password commit label wrapped onto two lines**
- **Found during:** Task 3 sweep
- **Issue:** beside the "Back to Users" exit the commit label did not fit on one line at phone widths.
- **Fix:** the shared stacked action row (`form-actions--stack`) on the phone view only; render fact pins that only the phone stacks.
- **Commit:** 6a829dbe

**3. [Plan wording] Role select option values**
- The plan's behaviour line says option values are "the enum names"; the form has always posted the enum numbers (`GetEnumSelectList`) and the shared mapping keeps them. Values unchanged, as the plan's prohibitions and the "unchanged option values" truth require.

**4. [Plan wording] Retiring the phone sheet**
- Task 1 said to keep `admin-form.mobile.css` linked until Task 2 if CreateUser or ResetPassword still needed it. The converted EditUser no longer used any of its classes, so it stopped linking it in Task 1; the file was deleted in Task 2 with the last consumer.

**Total deviations:** 2 auto-fixed, 2 plan-wording notes. **Impact:** none on behaviour beyond what Gate 1 answered.

## Test renames (recorded in tests-renamed.md)

- `MobileViewsTests.GetMobilePage_AdminEditUser_ReturnsSuccessAndMobileLayout` and `GetMobilePage_AdminResetPassword_ReturnsSuccessAndMobileLayout`: `admin-form-card-mobile` / `admin-form.mobile.css` became `form-page` / `admin-pages.mobile.css` (same intent: the phone page renders its phone markup and links its phone sheet; each also asserts the retired sheet is no longer linked). No test deleted.

## Residual notes

- `asp-validation-summary="All"` on ResetPassword repeats a field's message in the summary and under the field, as it did before; left as is (changing it would drop content the plan says to keep).
- The accessible-name keys in the sweep include the visually hidden marker text; real assistive technology reads "Name (required)" because the star is `aria-hidden`.

## Known Stubs

None.

## Threat Flags

None. T-100-52 (autofill) is mitigated: autocomplete off on Email and Name, `new-password` on both reset inputs. T-100-54 (CSRF) is covered by the guard's post-form scanner over the converted views. T-100-53 is closed rather than accepted: the client rule now matches the server policy.

## Next Phase Readiness

100-16 onward: the Admin area has only EmailStats left pending (100-20). The Platform views remain pending. `admin-pages.css` gained `.form-section-title`.

## Self-Check: PASSED

Commits 232b9d80, 130fdddc and 6a829dbe exist; the six views exist; `admin-form.mobile.css` is gone; full suites green (unit 1636, integration 2943 + 19 skipped).
