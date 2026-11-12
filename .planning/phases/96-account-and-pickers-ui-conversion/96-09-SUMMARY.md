---
phase: 96-account-and-pickers-ui-conversion
plan: 09
subsystem: ui
tags: [edit-profile, access-denied, mobile-twin, retired-sheet, overflow, guard-tests, tracer]
requires:
  - phase: 96-08
    provides: account.css with the account-link rule and refusal-line list rule, the shell root-row gutter rule, the guard test class
provides:
  - Account/Edit and Account/Edit.Mobile on the foundation card (still two twins)
  - Shared/AccessDenied on the foundation with one h1 and one alert
  - the phone account sheet deleted, no consumer left
  - a rewritten phone Edit fact, its desktop companion and a retired-sheet guard
affects: [96-10, 96-11, 96-12]
tech-stack:
  added: []
  patterns: [refusal line only when a model-level error exists, account-link target size on an in-text link]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Account/Edit.cshtml
    - QuestBoard.Service/Views/Account/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Account/ChangePassword.cshtml
    - QuestBoard.Service/Views/Shared/AccessDenied.cshtml
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/account.mobile.css
key-decisions:
  - "Gate 1 gutter answer 'shell' already shipped in 96-08, so AccessDenied keeps its row and column and takes no local width wrapper"
  - "The 44x22 'log in' link on the signed-out phone page is inside this plan's AccessDenied scope and is fixed by reusing the existing account-link class, no new rule"
  - "The three page-level action rows (both Edit twins and ChangePassword) gain gap-2 so the two buttons no longer touch at 390"
requirements-completed: [D-06, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8, DoC-10, X-4]
duration: ~1 h
completed: 2026-11-12
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 96 Plan 09: Edit twins, AccessDenied and the retired phone sheet Summary

Edit and AccessDenied are on the foundation, Change Password reads the same on both Edit twins, and the area's last unlayered phone sheet (`account.mobile.css`) is gone. AccessDenied fits every phone width signed in and out, and its one small tap target is now finger sized.

## What was built

- **Edit twins.** Both link `account.css`, use `.card.modern-card` with one `h1` Edit Profile and a decorative icon, and the unchanged form (`asp-action="Edit"`, hidden `Id`, antiforgery from the tag helper).
  - The refusal line (`role="alert"`, decorative `fa-circle-exclamation`, `asp-validation-summary="ModelOnly"`) renders only when `ModelState[""]` has errors, so a field-only error no longer draws an empty box.
  - Name carries `autocomplete="name"`, Email `autocomplete="email"`; validation spans carry no colour class.
  - `h2.section-heading` Password Management holds Change Password as `btn-secondary` (it was an outline link at 1.88:1 on desktop and a warning button on the phone). Only the desktop keeps the sentence "Want to change your password?".
  - Action row: Back to Profile (`btn-secondary`, same destination) left, Save Changes (`btn-primary`) right. The phone twin gains the document title Edit Profile.
- **Phone account sheet deleted.** `git grep account.mobile.css` now matches only the guard test's retired-file entry and the pre-existing `NotContain` assertions in `MobileViewsTests`. Its eleven important flags, fourteen rgb() and three hex literals left the area.
- **AccessDenied.** One `h1` Access Denied, `h2.section-heading` You Don't Have Permission (literal apostrophe), the sentence at body colour, one alert (info signed in, warning signed out), Back to Quest Board as `btn-secondary` with `d-grid gap-2 d-md-block`. Icons are decorative. The signed-out `log in` link carries `alert-link account-link`.

## AccessDenied scrollWidth (measured on 8000, stack mode)

| Page | 390 phone UA | 412 phone UA | 320 desktop UA | 1440 | text spacing |
|---|---|---|---|---|---|
| accessdenied-in | 390 (BEFORE 394) | 412 (BEFORE 416) | 320 | 1440 | no overflow |
| accessdenied-out | 390 | 412 | 320 | 1440 | no overflow |

**Evidence for the events phase:** `fx-events-details` (the shell rule from the credential slice) scrolls to 390 at 390, 412 at 412 and 320 at 320 (BEFORE 394 / 416). `smallTargets` is empty on every AccessDenied and Events/Details record.

## Facts rewritten, added

- `MobileViewsTests.MobileAccountEdit_MobileUserAgent_RendersGlassCardForm` (name kept, intent rewritten): phone UA, 200, `mobile-layout`, `modern-card`, `/css/account.css`, no `account.mobile.css`, `<title>Edit Profile - `, `autocomplete="name"`, no "Want to change your password?".
- New `MobileAccountEdit_DesktopUserAgent_KeepsThePasswordPrompt`: desktop UA, 200, prompt present, no `mobile-layout`.
- `AccountConversionGuardTests`: both Edit twins and AccessDenied in `ConvertedViews` and `SheetLinks`; both Edit twins in the autocomplete list (`name`, `email`); `account.mobile.css` in `RetiredFiles`; new `NoAreaViewLinksARetiredSheet`. `AccountSheets_DeclareNoTransitionOnAll` now requires both live sheets, since the phone sheet no longer exists.
- `AccountControllerIntegrationTests` AccessDenied facts pass unchanged. No test deleted.

## Sweep and readability-compare (`after/96-09`, stack mode)

- Edit and Edit errors: 8 captures, 0 non-200. `readability-compare --zero-contrast edit,edit-errors`: `failingRows: 0`, `residualContrast` 0, `ok`. The measured-selector check found nothing under 4.5:1 (the old outline link measured 1.88).
- AccessDenied signed in and out, plus `fx-events-details`: 11 captures, 0 non-200. `readability-compare --zero-contrast accessdenied-in,accessdenied-out`: `failingRows: 0`, `ok`; the overflow check printed `ok`.
- `allow.json`: `edit` and `edit-errors` only, `a||||link|Cancel` to `a||||link|Back to Profile`. AccessDenied needed no allow entry.
- The edit-errors row shows only the field message, no summary box (screenshot checked at 390).

## Deviations from Plan

### Environment substitutions (operator-approved, see ENVIRONMENT.md)

Port 8000 for 8096, `python3` for `python`, bundled Chromium, `qb-audit-*` accounts, `--stack` on every sweep, `--merge` on the second sweep.

### Auto-fixed issues

**1. [Rule 2 - Missing critical] The 44x22 `log in` link on AccessDenied signed out.**
- **Found during:** Task 2 (carried from the 96-08 note)
- **Issue:** an inline link under the 44px target on the phone.
- **Fix:** added the existing `account-link` class; no new CSS. `smallTargets` is now empty at 390 and 412.
- **Files modified:** `QuestBoard.Service/Views/Shared/AccessDenied.cshtml`
- **Commit:** 12fa60d3

**2. [Rule 1 - Bug] The two action buttons touched at 390.**
- **Found during:** Task 1 screenshot review
- **Issue:** `d-flex justify-content-between` with no gap left Back to Profile and Save Changes edge to edge.
- **Fix:** `gap-2` on the action row of both Edit twins and, for the same look, ChangePassword.
- **Files modified:** `Edit.cshtml`, `Edit.Mobile.cshtml`, `ChangePassword.cshtml`
- **Commit:** 2688a844

### Plan wording and process

- **Tracer gate.** Neither auto-advance flag is set; as in 96-06 to 96-08 the gate was met by running the tracer's own verify end to end (276 pinned tests green, sweep and compare `ok`) before Task 2. Operator review stays at Gate 2.
- **`account.css` and `gutter`.** The Gate 1 answer is `shell`, so no wrapper rule was added to `account.css` and it is not in this plan's modified files.
- **Backstops not run in a browser:** the email-change confirmation round trip after a real Edit submit (the form, action and bound model are unchanged and the controller tests pass).

## Findings closed

The plan's findings A-account-016 to A-account-018, A-account-038 to A-account-042, A-account-006, A-account-007 and A-account-011: the Edit contrast, button-role and autofill fixes, the AccessDenied headings, alert and overflow fix (X-4), and the retired phone sheet. The per-finding wording was not re-read from the research file; the closing evidence is the sweep and test results above.

## Known Stubs

None.

## Threat Flags

None new. T-96-25: the form tag helper still emits the antiforgery token, the hidden `Id` and the bound view model are unchanged, only markup and classes moved. T-96-26: AccessDenied copy is static and `AccountControllerIntegrationTests` still asserts no `Dungeon Master`.

## Verification results

- `dotnet build`: 0 errors.
- Full `dotnet test` with the SQL-backed tests required: unit 1389 passed, integration 1889 passed, 0 failed.
- App on 8000 restarted on the final code and left running.

## Self-Check: PASSED

- Views present: Edit, Edit.Mobile, AccessDenied; `account.mobile.css` is gone.
- Commits 2688a844 (Task 1) and 12fa60d3 (Task 2) exist on `milestone/v9-rolling-improvements`.
- `readability-compare` printed `ok` for both tasks; the Edit contrast and AccessDenied overflow checks printed `ok`.
