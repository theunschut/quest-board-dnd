---
phase: 100-admin-and-platform-ui-conversion
plan: 14
subsystem: ui
tags: [razor, refused-controls, role-badge, layered-css, guard-tests, admin-users, mobile-twins]
requires:
  - phase: 100-13
    provides: AdminPlatformDisplay mapping, .control-reason and .row-slip, admin-pages sheets, the area guard lists
  - phase: 100-06
    provides: IUserService.GetAccountReachAsync and the reach rule
  - phase: 100-09
    provides: the self and last-Admin rules for role changes
  - phase: 100-10
    provides: the self and last-Admin rules for removal
provides:
  - UserManagementViewModel Role, IsSelf, IsLastAdmin and Reach per row, filled from one reach call and one Admin count
  - _RoleBadge, the one group-role badge partial (model GroupRole) for both layouts
  - Admin/Users on both layouts per the approved drawings, with refused controls and visible reason lines
  - AdminActionMessages reason sentences and the corrected Remove from Group confirm sentence
affects: [100-15, 100-16, 100-18, 100-19]
tech-stack:
  added: []
  patterns: [refused control = type=button + aria-disabled + aria-describedby to one reason line per refusal kind per row, outside any form; per-row flags computed from the same Domain rules that refuse a crafted POST]
key-files:
  created:
    - QuestBoard.Service/Views/Shared/_RoleBadge.cshtml
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/UserManagementViewModel.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs
    - QuestBoard.Service/Views/Admin/Users.cshtml
    - QuestBoard.Service/Views/Admin/Users.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/admin-pages.css
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
    - .planning/phases/100-admin-and-platform-ui-conversion/100-SECURITY.md
  deleted:
    - QuestBoard.Service/wwwroot/css/admin-users.mobile.css
key-decisions:
  - "Reason sentences are the audit-approved role-free ones (audit-answers.json); no sentence carries the mapped role word, so none is recorded as such in the security record"
  - "A row that is both the viewer's own and the board's only Admin shows the self sentence only; the last-Admin line shows only on another person's row"
  - "Role buttons follow the approved colours: Promote success, Demote secondary, Resend info, Edit warning, Remove danger; a refused control keeps its class plus disabled so the neutral refused fill applies"
  - "The Remove from Group confirm sentence is the corrected one (Gate 1 item 8 yes); the one existing fact that pinned the old sentence was updated"
patterns-established:
  - "The Users page asks GetAccountReachAsync once for all rows and CountMembersWithRoleAsync once; the view holds no rule of its own"
requirements-completed: [D-02, D-03, D-13, D-15, D-16, D-19, D-21, DoC-1, DoC-4, DoC-8, DoC-10]
duration: ~1h
completed: 2026-11-18
status: complete
actuals:
  tokens: 14800
  tasks: 3
  commits: 3
---

# Phase 100 Plan 14: Admin Users Summary

**Admin/Users on both layouts now follows the approved drawings: the email sits under the name, the five controls keep a fixed order, the role badge comes from the shared mapping through `_RoleBadge`, and every control the server would refuse (own row, only Admin, out-of-reach account) renders as a focusable aria-disabled button with a visible lock-and-sentence reason, driven by per-row flags computed from the same rules that refuse a crafted post.**

## What was built

- **Task 1 (tracer, 74de3d1d):** `UserManagementViewModel` gained `IsSelf`, `IsLastAdmin` and `Reach`; `AdminController.Users` resolves the acting user, calls `GetAccountReachAsync` once for all rows and `CountMembersWithRoleAsync` once. The self row's role button and Remove from Group became refused buttons with one reason line on both layouts. The tracer gate was verified by the render facts and the Admin role, controller and render suites (63 green) before expanding.
- **Task 2 (0628dc53):** both views rewritten per the drawings. Desktop: page-title card with one `h1` and `Create User` as the single `btn-primary`; columns Name, Current Role, Actions; email as a `mailto` `email-link` under the name (or `No email provided` in muted ink); right-aligned Actions with the controls wrapping inside the cell. Phone: one `.row-slip` per person with name and badge (no address), the same forms in the same order at 44px, now including the Resend Welcome Email form. `_RoleBadge` created. Refusals: self (every role button, Remove), last Admin (demotion, Remove), reach (Edit, Resend Welcome Email), each with its own reason line referenced by every refused control of that kind. `admin-users.mobile.css` deleted; the Users pair and `_RoleBadge` moved to ConvertedViews, the sheet to RetiredFiles; the MobileViewsTests Users entry rewritten with the same intent; page rules added to `admin-pages.css`.
- **Task 3 (this commit):** both suites, the sweep against BEFORE, the two afterOnly manifest rows, this summary and the docs commit.

## Deviations from Plan

### Auto-fixed / judgement calls

**1. [Rule 2 - Missing critical functionality] Corrected Remove from Group sentence and its existing pin**
- **Found during:** Task 2
- **Issue:** Gate 1 item 8 was answered yes, so the confirm sentence on both layouts is the corrected one. `AdminControllerIntegrationTests` pinned the old sentence.
- **Fix:** the sentence lives in `AdminActionMessages.RemoveMemberConfirm`; the fact's expected string was updated (recorded in tests-renamed.md); all other assertions on the form are unchanged.
- **Commit:** 0628dc53

**2. [Rule 2] `Role` added to `UserManagementViewModel`**
- The plan names IsSelf, IsLastAdmin and Reach; the view also needs the group role to hand to `_RoleBadge` without branching on enum members (the converted-view guard forbids that), so the controller fills `Role` from the role it already reads.

**3. [Rule 1 - Bug in test seed] Empty-state fact uses a platform administrator who is not a member**
- A board always has the viewing Admin as a member, so the empty state is reachable only by a SuperAdmin who is not on the board (the same way the sweep row `admin-users-empty` reaches it).

**4. Email link target on desktop:** `.user-cell__email` now carries `padding-block` in place of its top margin so the link target is at least 24px; the Admin Quests DM address uses the same rule and grows by 4px.

### Sweep note

The sweep's own `admin-users-focus-refused` frame does not show the ring (the action's focus is dropped before the full-page capture, a harness matter; a direct probe shows the focused refused button with `solid 2px` dark outline plus a 2px gold box-shadow, desktop and phone). The two-tone ring crops are kept as `after/100-14/desktop/admin-users-focus-refused-ring.png` and `mobile/...-ring.png`.

## Verification

- `dotnet build` green. Unit 1636 passed. Integration 2921 passed, 19 skipped (the same open-gap reproduction facts as before), 0 failed.
- New facts: 22 render cases in `AdminPlatformRenderTests` (self row, other rows, only Admin, reach, role badge words, one title and sheets, desktop columns and email under the name, phone slips without address, Resend rules, control order and Remove form, empty states, long and empty names), each on both User-Agents where the layout differs.
- Sweep (`$QBR/after/100-14`, 8340) of `admin-users`, `admin-users-empty`, `admin-users-lastadmin`, `admin-users-focus-refused` under both User-Agents: all 200, no console or page error, no overflow at 320, no contrast failure, no small target, no missing focus ring. `readability-compare.py` against BEFORE for `admin-users` and `admin-users-empty`: 10 compared, 0 failing, "ok". The only allow entries are the key renames from refused buttons (`button` type for a refused control, `submit` for the working Remove) and the ordinal shifts that follow. Both layouts at 1024 and 1440: the five-button row wraps inside its cell and the table scrolls inside `.table-responsive`.

## Threat model

T-100-49 (a refused control that still submits): refused controls are `type="button"`, outside every form; render facts assert no form ancestor and no title; the crafted-post refusals of the earlier plans are unchanged. T-100-50: the reason is the approved generic sentence, no board name. T-100-51: Razor encoding only; ids in aria attributes are numeric.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- `_RoleBadge.cshtml` exists; `admin-users.mobile.css` is gone; commits 74de3d1d and 0628dc53 exist on the branch.
