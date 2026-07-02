---
phase: 30-group-ux-admin-user-creation
plan: 03
subsystem: auth
tags: [aspnet-core-mvc, identity, hangfire, group-roles, multi-tenancy, razor]

# Dependency graph
requires:
  - phase: 30-group-ux-admin-user-creation
    provides: "30-01 GetGroupsForUserAsync, SessionKeys.ActiveGroupName, GroupPickerController scaffold (used as the redirect target for null-active-group guards)"
provides:
  - "AdminController.CreateUser GET/POST — admin-only account creation assigned to the admin's active group"
  - "CreateUserViewModel (Email, Name, Password, GroupRole)"
  - "CreateUser.cshtml + CreateUser.Mobile.cshtml admin views"
  - "?? 1 fallback removed from AdminController.Users() and UserRepository.GetAllPlayers/GetAllDungeonMasters"
affects: [30-04-nav-group-switch, 30-05-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "CreateUser POST reads target group from server-side IActiveGroupContext.ActiveGroupId, never from the submitted form, closing an IDOR path for cross-group user placement"
    - "Null ActiveGroupId guard pattern: redirect to GroupPicker/Index instead of defaulting to group 1 (replaces all Phase 28/29 ?? 1 fallbacks)"
    - "List-returning repository methods with no controller to redirect return an empty list on null ActiveGroupId rather than throwing or defaulting"

key-files:
  created:
    - QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs
    - QuestBoard.Service/Views/Admin/CreateUser.cshtml
    - QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Repository/UserRepository.cs

key-decisions:
  - "CreateUser.Mobile.cshtml follows the EditUser.Mobile.cshtml admin-form-card-mobile pattern (same controller, same CSS file) rather than the Login.Mobile.cshtml account-card-mobile pattern referenced in the plan text — closer, already-established analog within Views/Admin/"
  - "UserRepository.GetAllDungeonMasters/GetAllPlayers return an empty list (not an exception) when ActiveGroupId is null — these methods have no controller to redirect from, so an empty list is the safe no-data state"

requirements-completed: [MGMT-07, MGMT-08, REG-02, REG-03]

# Metrics
duration: 20min
completed: 2026-06-30
status: complete
---

# Phase 30 Plan 03: Admin User Creation & ?? 1 Fallback Removal Summary

**AdminController.CreateUser assigns new accounts to the admin's active group with a chosen GroupRole and queues the existing confirmation email job; all three Phase 28/29 `?? 1` group fallbacks are removed**

## Performance

- **Duration:** 20 min
- **Tasks:** 3
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments
- `CreateUserViewModel` (Email, Name, Password, GroupRole defaulting to Player) plus desktop and mobile `CreateUser` views following the existing `EditUser` modern-card / admin-form-card-mobile patterns
- `AdminController.CreateUser` GET/POST: validates the form, creates the Identity account via `userService.CreateAsync`, assigns the new user to the admin's `activeGroupContext.ActiveGroupId` (server-side, never the form) with the chosen `GroupRole` via `SetGroupRoleAsync`, generates and enqueues the existing `ConfirmationEmailJob` exactly as the old self-registration flow did, and redirects to `Users` with a TempData success message (MGMT-07, REG-02, REG-03)
- `AdminController.Users()` no longer falls back to group 1 — a null `ActiveGroupId` now redirects to `GroupPicker/Index` instead of silently scoping to the wrong group (D-17)
- `UserRepository.GetAllDungeonMasters` / `GetAllPlayers` no longer fall back to group 1 — a null `ActiveGroupId` returns an empty list, since these methods have no controller context to redirect from (D-17)
- Promote/demote actions (`PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer`) already used the null-guard pattern from Phase 29 and required no changes — they now work correctly against the real active group now that the fallback is gone elsewhere in the file (MGMT-08)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create CreateUserViewModel and Create User views (desktop + mobile)** - `12de7b5` (feat)
2. **Task 2: Add AdminController.CreateUser GET/POST and remove ?? 1 from Users()** - `2d182b1` (feat)
3. **Task 3: Remove ?? 1 fallback from UserRepository.GetAllPlayers and GetAllDungeonMasters** - `f9c8769` (fix)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` - Email/Name/Password/GroupRole with Data Annotations, modeled on RegisterViewModel
- `QuestBoard.Service/Views/Admin/CreateUser.cshtml` - desktop create-user form, modern-card layout, GroupRole `<select>` via `Html.GetEnumSelectList<GroupRole>()`
- `QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml` - mobile create-user form, admin-form-card-mobile wrapper
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - added `CreateUser` GET/POST actions; removed `?? 1` fallback and stale TODO comment from `Users()`, added null-guard redirect to `GroupPicker`
- `QuestBoard.Repository/UserRepository.cs` - removed `?? 1` fallback and stale TODO comments from `GetAllDungeonMasters`/`GetAllPlayers`; null `ActiveGroupId` now returns an empty list

## Decisions Made
- Followed `EditUser.Mobile.cshtml` (the actual sibling file in `Views/Admin/`, using `admin-form-card-mobile` + `admin-form.mobile.css`) instead of the plan's literal `Login.Mobile.cshtml account-card-mobile` reference, since it is the closer existing analog for an admin form and keeps the mobile admin views visually consistent with each other
- `UserRepository` list methods return `[]` rather than throwing when `ActiveGroupId` is null, since there is no controller layer to redirect from at the repository level

## Deviations from Plan

None - plan executed exactly as written (the mobile-view analog choice above is a refinement within the plan's own intent, not a deviation from a `must_haves` requirement).

## Issues Encountered

The repo's solution file is `QuestBoard.slnx` (not `QuestBoard.sln` as referenced in the plan's verification commands) — this is a pre-existing naming difference noted in the 30-01 summary as well. Used `dotnet build QuestBoard.slnx` / `dotnet test` against the actual file; build succeeded with 0 errors after each task and all 8 `AdminController` integration tests passed.

A full `dotnet test QuestBoard.slnx` run surfaced one pre-existing, unrelated failure: `GroupManagementIntegrationTests.AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow` (Platform area `GroupController.AddMember`, added in Phase 29). Verified this fails identically on a clean working tree (no diff from this plan's 3 commits) — out of scope for plan 30-03 per the executor scope-boundary rule. Logged to `.planning/phases/30-group-ux-admin-user-creation/deferred-items.md` for follow-up.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Admin-only user creation is fully wired: a group admin can create an account, assign a GroupRole within their active group, and the existing email-confirmation job fires — ready for plan 30-05 to add CreateUser-specific tests
- All three Phase 28/29 `?? 1` fallbacks (`AdminController.Users()`, `UserRepository.GetAllDungeonMasters`, `UserRepository.GetAllPlayers`) are now removed; no `?? 1` token remains in either file (grep gate verified)
- `QuestController.cs` still has one unrelated `?? 1` for session reminder enqueueing — explicitly out of scope per the plan and STATE.md notes
- A pre-existing, unrelated integration test failure (`GroupManagementIntegrationTests.AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow`) is tracked in `deferred-items.md` for a future phase/plan to investigate
- No blockers for plan 30-04 (nav group switch) or 30-05 (tests)

---
*Phase: 30-group-ux-admin-user-creation*
*Completed: 2026-06-30*

## Self-Check: PASSED

All 5 claimed files verified present on disk; all 3 commit hashes verified present in git log.
