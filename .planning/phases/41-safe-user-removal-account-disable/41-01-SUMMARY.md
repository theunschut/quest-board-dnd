---
phase: 41-safe-user-removal-account-disable
plan: 01
subsystem: auth
tags: [aspnet-core-identity, ef-core, group-membership, admin-ui]

# Dependency graph
requires:
  - phase: 40-platform-members-page-redesign
    provides: IGroupService.RemoveMemberAsync primitive already proven via GroupController.RemoveMember
provides:
  - AdminController.DeleteUser repurposed to group-scoped removal instead of account hard-delete
  - "Remove from Group" UI copy replacing hard-delete "Delete" copy on Admin Users page (desktop + mobile)
  - Integration test coverage proving group-only removal and FK-history no-throw behavior
affects: [41-02, 41-03, 41-04]

# Tech tracking
tech-stack:
  added: []
  patterns: [reuse existing IGroupService.RemoveMemberAsync primitive instead of adding new service method]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/Views/Admin/Users.cshtml
    - QuestBoard.Service/Views/Admin/Users.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs

key-decisions:
  - "DeleteUser now calls groupService.RemoveMemberAsync(groupId.Value, id) instead of userService.RemoveAsync(user) — reuses the identical primitive GroupController.RemoveMember already uses, no new service method needed"
  - "No special-case handling added for a user left with zero group memberships — GroupSessionMiddleware already redirects gracefully to /groups/pick, confirmed non-issue per plan objective"
  - "JS function renamed deleteUser -> removeFromGroup in both desktop and mobile views for clarity, alongside the button/copy rename"

patterns-established:
  - "Group-scoped account actions call groupService.RemoveMemberAsync(groupId, userId) rather than mutating the UserEntity row directly — preserves account + other-group memberships"

requirements-completed: [SAFE-01]

# Metrics
duration: 5min
completed: 2026-07-04
status: complete
---

# Phase 41 Plan 01: Safe Group-Only User Removal Summary

**Repurposed the group-admin "Delete" user action to a reversible group-scoped removal via `IGroupService.RemoveMemberAsync`, replacing the hard-delete that previously cascaded a user out of every group and threw an unhandled `DbUpdateException` for users with quest/shop/transaction/reminder history.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-07-04T12:35:57Z
- **Completed:** 2026-07-04T12:41:00Z
- **Tasks:** 3 completed
- **Files modified:** 4

## Accomplishments
- `AdminController.DeleteUser` now removes only the active-group `UserGroupEntity` row instead of hard-deleting the `UserEntity` account
- Added two integration tests proving: (1) the account and any other group memberships survive removal, and (2) removal does not throw for a user with rows across all five `NoAction` FKs (quest DM, shop item creator, transaction, reminder log) and does not silently cascade-delete their character/signup rows
- Renamed the Admin Users page "Delete" button to "Remove from Group" (icon `fa-trash` → `fa-user-minus`) with accurate, reversible-action confirm copy, applied identically to desktop and mobile views

## Task Commits

Each task was committed atomically:

1. **Task 1: Add DeleteUser integration tests proving group-only removal (Wave 0 scaffold)** - `20c1185` (test)
2. **Task 2: Repurpose AdminController.DeleteUser to group-scoped removal (D-01)** - `171131b` (feat)
3. **Task 3: Rename Delete button + confirm copy to "Remove from Group" (D-02, desktop + mobile)** - `dc93278` (feat)

_Note: Task 1 tests were RED (failing) until Task 2 landed, then GREEN, per the plan's Wave 0 scaffold design._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - `DeleteUser` now calls `groupService.RemoveMemberAsync(groupId.Value, id)` instead of `userService.RemoveAsync(user)`; both group-scoping guards and `[HttpDelete]`/`[ValidateAntiForgeryToken]` attributes unchanged
- `QuestBoard.Service/Views/Admin/Users.cshtml` - Delete button relabeled "Remove from Group" with `fa-user-minus` icon; `deleteUser()` JS renamed `removeFromGroup()` with updated confirm copy
- `QuestBoard.Service/Views/Admin/Users.Mobile.cshtml` - Identical rename applied to the mobile card action row
- `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` - Added `DeleteUser_Post_RemovesGroupMembershipOnly_AccountAndOtherMembershipsIntact` and `DeleteUser_Post_WithQuestShopTransactionReminderHistory_DoesNotThrow`

## Decisions Made
- Reused the existing `IGroupService.RemoveMemberAsync(groupId, userId)` primitive verbatim rather than introducing any new service method — matches `GroupController.RemoveMember`'s established call shape exactly, per the plan's pattern assignment
- No DI changes were needed: `IGroupService groupService` was already a constructor parameter on `AdminController`

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

One pre-existing, out-of-scope test (`SendConfirmationEmail_DifferentTargetUsers_ShouldHaveIndependentBudgets`) intermittently fails when the full `AdminControllerIntegrationTests` class runs together, due to test-order interaction with a process-wide singleton `PartitionedRateLimiter` shared across the whole test run — confirmed to pass in isolation, and confirmed unrelated to any file this plan touches. Not fixed here (out of scope per plan's `<files_modified>` list); left for a future test-isolation cleanup pass if it becomes disruptive.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

SAFE-01 is fully satisfied: group-admin removal is now reversible and safe for users with any FK history. Plans 41-02/41-03/41-04 (SuperAdmin account-disable via Identity's `LockoutEnd` mechanism) are unaffected by and independent of this change — `IIdentityService`/`IdentityService` were not touched in this plan.

---
*Phase: 41-safe-user-removal-account-disable*
*Completed: 2026-07-04*
