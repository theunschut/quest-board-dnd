---
phase: 49-fix-guild-members-page-missing-group-tenant-filtering
plan: 02
subsystem: auth
tags: [authorization, multi-tenancy, dungeonmaster, integration-tests]

# Dependency graph
requires:
  - phase: 49-01
    provides: Cross-group leak fix pattern (target-group-membership check via GetGroupRoleByIdAsync) established on an earlier controller in this phase
provides:
  - Target-group-membership check on DungeonMasterController's Profile / EditProfile (GET+POST) / GetDMProfilePicture actions
  - IsTargetInActiveGroupAsync private helper centralizing the check
  - Integration test coverage for cross-group 404 and SuperAdmin-no-group 404 across all four actions
affects: [49-03, 49-04, future DM profile / group management phases]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Target-group-membership gate: a private controller helper calls IUserService.GetGroupRoleByIdAsync(targetUserId, activeGroupId) and returns false (→ NotFound()) both for non-members and for a null ActiveGroupId, placed before any existing same-tenant Forbid() check or data mutation."

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
    - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs

key-decisions:
  - "Reused the existing IUserService.GetGroupRoleByIdAsync primitive (same one AdminController already uses) instead of adding new repository plumbing."
  - "Returned NotFound() (404) rather than Forbid() (403) for cross-group targets, matching the phase-wide convention of hiding cross-tenant existence."
  - "Left DungeonMasterProfileEntity/Service untouched — the membership gate lives entirely in the controller since profile data is intentionally shared across a DM's groups."
  - "Fixed three pre-existing integration tests (EditProfile_AdminEditingOtherDm_ReturnsOk, EditProfile_NonAdminDmEditingOtherDm_ReturnsForbidden, Profile_NonOwnerAdmin_SeesEditProfileMarker, EditProfile_Player_IsForbiddenOrRedirected) whose 'other DM' target user had zero group membership at all under the old code — now they seed group-1 membership to preserve their original same-group intent under the new check."
  - "The SuperAdmin-no-active-group test authenticates as a SuperAdmin (not a plain DungeonMaster) because GroupSessionMiddleware redirects any other authenticated role to /groups/pick before the request ever reaches the controller when ActiveGroupId is null; SuperAdmin is the one role the middleware exempts, so it's the only way to actually exercise the controller-level null-group 404."

patterns-established:
  - "Any controller with a target user id + IActiveGroupContext should gate on target-group-membership before touching same-tenant Forbid()/data-write logic — same shape used in 49-01 and here."

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-07-05
status: complete
---

# Phase 49 Plan 02: DungeonMasterController Cross-Group Leak Fix Summary

**Closed the DungeonMasterController cross-group leak (D-06 through D-09) by adding a target-group-membership check — via the existing GetGroupRoleByIdAsync primitive — to Profile, both EditProfile overloads, and GetDMProfilePicture, returning 404 before any Forbid() or profile write.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-05T18:31:32Z
- **Completed:** 2026-07-05T18:57:39Z
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments

- Added a private `IsTargetInActiveGroupAsync(int targetUserId)` helper on `DungeonMasterController` that short-circuits to `false` when `ActiveGroupId` is null, otherwise checks `GetGroupRoleByIdAsync(targetUserId, groupId) != null`
- Wired the check into all four actions (`Profile`, `EditProfile` GET, `EditProfile` POST, `GetDMProfilePicture`), placed before the existing same-tenant `Forbid()` and before `UpsertProfileAsync`
- The POST path is the most severe fix: an Admin in one group can no longer overwrite the bio/profile picture of a DM in an unrelated group
- Added 5 new integration tests covering cross-group 404 for all four actions (including a POST test that asserts the bio is not persisted) plus a SuperAdmin-no-active-group 404 test
- Fixed 4 pre-existing integration tests whose "other DM" target users had no group membership at all, which is now correctly rejected by the membership gate — updated them to seed group-1 membership so they continue to test their original same-group scenarios
- Full `QuestBoard.IntegrationTests` suite (298 tests) passes with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add target-group-membership check to Profile/EditProfile(GET+POST)/GetDMProfilePicture** - `5e2bd0d` (feat)
2. **Task 2: Add cross-group and SuperAdmin-no-group integration tests for DM profile actions** - `64a53cf` (test)

_Note: TDD task type was set on both tasks, but the codebase and test project already existed, so both tasks were executed as source+test edits with build/test verification rather than a strict separate RED-commit / GREEN-commit split — this mirrors the plan's own phrasing ("Add task", not "write failing test first") and the existing DungeonMasterController test file's established single-commit-per-feature convention._

## Files Created/Modified

- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` - Added `IsTargetInActiveGroupAsync` helper; wired `NotFound()` checks into `Profile`, `EditProfile` (GET+POST), `GetDMProfilePicture`
- `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs` - Added `IAsyncLifetime` group-context reset, an `AddUserToGroupAsync` test helper, 5 new cross-group/SuperAdmin-no-group tests, and fixed 4 existing tests to seed group-1 membership for their target users

## Decisions Made

- Used the existing `GetGroupRoleByIdAsync` primitive rather than adding new repository plumbing — no schema change, no new service method.
- Returned 404 uniformly for cross-group targets (never 403), consistent with the phase's established existence-hiding convention.
- Test for the SuperAdmin-no-active-group case had to authenticate as SuperAdmin specifically, not just any authenticated role, because `GroupSessionMiddleware` intercepts and redirects any non-SuperAdmin authenticated request with a null `ActiveGroupId` before it reaches the controller — this is existing, unrelated middleware behavior, not something this plan changed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed 4 pre-existing integration tests broken by the new target-group-membership check**
- **Found during:** Task 2 (writing new integration tests, then running the full class to verify)
- **Issue:** `EditProfile_AdminEditingOtherDm_ReturnsOk`, `EditProfile_NonAdminDmEditingOtherDm_ReturnsForbidden`, `Profile_NonOwnerAdmin_SeesEditProfileMarker`, and `EditProfile_Player_IsForbiddenOrRedirected` all constructed their "other DM"/"target DM" user via `AuthenticationHelper.CreateTestUserAsync`, which creates a user with **no** `UserGroups` row at all. Before this plan's fix, that didn't matter because nothing checked the target's group membership. After adding `IsTargetInActiveGroupAsync`, these targets are correctly treated as not-in-group-1 and now 404 — which broke the tests' original same-group assertions (expecting 200/403 rather than 404).
- **Fix:** Added a small `AddUserToGroupAsync` test helper and called it for each of these targets to seed a group-1 `UserGroupEntity` row, restoring the tests' original same-group intent under the corrected behavior.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs`
- **Verification:** All 4 tests pass again with the membership seeded; full `DungeonMasterControllerIntegrationTests` class (15 tests) and full `QuestBoard.IntegrationTests` suite (298 tests) pass.
- **Committed in:** `64a53cf` (Task 2 commit)

**2. [Rule 1 - Bug] SuperAdmin-no-group test needed a SuperAdmin viewer, not a DungeonMaster**
- **Found during:** Task 2 (first test run after writing `Profile_SuperAdminNoActiveGroup_ReturnsNotFound`)
- **Issue:** The test initially authenticated as a `DungeonMaster` role viewer and set `factory.TestGroupContext.ActiveGroupId = null`, expecting the controller's new null-group 404 to fire. Instead it got a 302 redirect, because `GroupSessionMiddleware` intercepts any authenticated non-SuperAdmin request with a null `ActiveGroupId` and redirects to `/groups/pick` before the request ever reaches `DungeonMasterController`.
- **Fix:** Changed the test to authenticate as `SuperAdmin` (the one role the middleware exempts from this redirect), so the request reaches the controller and exercises `IsTargetInActiveGroupAsync`'s null-group branch directly.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs`
- **Verification:** Test passes with `SuperAdmin` role; full test suite green.
- **Committed in:** `64a53cf` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — bugs in the tests themselves, not the plan or its acceptance criteria)
**Impact on plan:** Both fixes were necessary to make the plan's own acceptance criteria (existing same-group tests remain green) actually hold under the corrected security behavior. No scope creep — no production code changes beyond what the plan specified.

## Issues Encountered

None beyond the deviations documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `DungeonMasterController` is fully closed for the cross-group leak described in D-06 through D-09; `DungeonMasterProfileEntity`/`Service` remain unchanged (D-09a).
- The `IsTargetInActiveGroupAsync` pattern (short-circuit on null `ActiveGroupId`, then `GetGroupRoleByIdAsync`) is now demonstrated on two controllers in this phase (49-01's target and this plan's `DungeonMasterController`) and can be reused directly by sibling plans in this wave if they encounter the same "check caller, not target" shape.
- Full integration test suite (298 tests) passes; no known blockers for 49-03/49-04.

---
*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Completed: 2026-07-05*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
- FOUND: .planning/phases/49-fix-guild-members-page-missing-group-tenant-filtering/49-02-SUMMARY.md
- FOUND commit: 5e2bd0d (Task 1)
- FOUND commit: 64a53cf (Task 2)
- FOUND commit: 1fc690c (docs: plan completion)
