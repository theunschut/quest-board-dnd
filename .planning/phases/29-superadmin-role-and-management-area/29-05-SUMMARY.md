---
phase: 29-superadmin-role-and-management-area
plan: "05"
subsystem: testing
tags: [integration-tests, superadmin, authorization, platform-area, group-management, xunit]
dependency_graph:
  requires:
    - Phase 29-01 (SuperAdminOnly policy, AdminHandler/DungeonMasterHandler with SuperAdmin bypass)
    - Phase 29-02 (SuperAdmin Identity role migration — role exists in DB)
    - Phase 29-03 (IGroupService/IGroupRepository — group CRUD and member management)
    - Phase 29-04 (GroupController + /platform area routes, five Razor views)
  provides:
    - AdminHandlerIntegrationTests: 8 tests covering AUTH-02, AUTH-03, AUTH-04
    - PlatformAreaIntegrationTests: 4 tests covering AUTH-05 + MGMT-01
    - GroupManagementIntegrationTests: 10 tests covering MGMT-02 through MGMT-06
    - TestDataHelper.SeedRolesAsync seeds SuperAdmin alongside Admin/DungeonMaster/Player
    - AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync helper method
  affects:
    - Phase 30 (Group UX — these test patterns are the regression gate for auth)
tech_stack:
  added: []
  patterns:
    - TestDataHelper.ClearDatabaseAsync called in each test that needs clean state
    - DbContext direct seeding for UserGroups rows (scope.ServiceProvider.GetRequiredService<QuestBoardContext>())
    - CreateAuthenticatedSuperAdminClientAsync wraps CreateAuthenticatedClientWithUserAsync with ["SuperAdmin"]
    - xUnit IClassFixture<WebApplicationFactoryBase> pattern for all test classes
key_files:
  created:
    - QuestBoard.IntegrationTests/Controllers/AdminHandlerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
    - QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs
key-decisions:
  - "AdminHandlerIntegrationTests uses /DungeonMaster/EditProfile for AUTH-03 (DungeonMasterOnly) tests — existing DM-only route; no new route needed"
  - "GroupManagementIntegrationTests.DeleteGroup_WhenHasMembers uses a freshly-created group with a seeded member (not Group 1) — avoids test isolation issues with the shared EuphoriaInn group"
  - "AddMember test removes pre-existing UserGroups row seeded by CreateAuthenticatedClientWithUserAsync before testing the AddMember endpoint — ensures clean state for membership assertion"
patterns-established:
  - "Pattern: Direct DbContext seeding in test method for UserGroups rows (CreateScope + GetRequiredService<QuestBoardContext>)"
  - "Pattern: CreateAuthenticatedSuperAdminClientAsync for any test requiring SuperAdmin access"
requirements-completed:
  - AUTH-01
  - AUTH-02
  - AUTH-03
  - AUTH-04
  - AUTH-05
  - MGMT-01
  - MGMT-02
  - MGMT-03
  - MGMT-04
  - MGMT-05
  - MGMT-06

# Metrics
duration: 8min
completed: 2026-06-30
---

# Phase 29 Plan 05: Integration Tests for SuperAdmin Auth and Group Management

22 new integration tests across 3 classes validate all Phase 29 requirements — AdminHandler/DungeonMasterHandler auth policies (AUTH-02–04), SuperAdminOnly platform area (AUTH-05, MGMT-01), and full group CRUD + member add/remove (MGMT-02–06) — with 219 total tests passing (0 failures).

## Performance

- **Duration:** 8 min
- **Started:** 2026-06-30T13:40:00Z
- **Completed:** 2026-06-30T13:48:00Z
- **Tasks:** 2 (+ checkpoint)
- **Files modified:** 5

## Accomplishments

- All five test files exist and pass: TestDataHelper updated, AuthenticationHelper updated, three new integration test classes created
- AdminHandlerIntegrationTests (8 tests): verifies GroupRole.Admin → 200 on AdminOnly, GroupRole.Player → 403/redirect, GroupRole.DungeonMaster → 200 on DungeonMasterOnly, GroupRole.Admin → 200 on DungeonMasterOnly, GroupRole.Player → 403/redirect on DungeonMasterOnly, SuperAdmin → 200 on AdminOnly (AUTH-04), SuperAdmin → 200 on DungeonMasterOnly (AUTH-04), unauthenticated → redirect
- PlatformAreaIntegrationTests (4 tests): SuperAdmin → 200 on /platform/Group/Index, Player → 403/redirect, Admin → 403/redirect, unauthenticated → redirect
- GroupManagementIntegrationTests (10 tests): groups index returns 200 with "Group Management" + "EuphoriaInn", create with unique name succeeds, created group appears in index, delete confirmation page for empty group returns 200, delete POST for empty group redirects, delete of group with members redirects (HasMembersAsync guard), Members page returns 200, AddMember adds UserGroups row, RemoveMember deletes UserGroups row
- Full suite: 219 tests (55 unit + 164 integration), 0 failures — up from 197 before this plan

## Task Commits

1. **Task 1: Update TestDataHelper, AuthenticationHelper, write three integration test classes** - `7ee6ef9` (feat)
2. **Task 2: Full test suite gate** - verification only (no code changes, 219 tests pass)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — SeedRolesAsync now includes "SuperAdmin" role (was already modified before plan execution started)
- `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` — CreateAuthenticatedSuperAdminClientAsync static method added (was already present before plan execution)
- `QuestBoard.IntegrationTests/Controllers/AdminHandlerIntegrationTests.cs` — 8 integration tests for AUTH-02/03/04
- `QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs` — 4 integration tests for AUTH-05/MGMT-01
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — 10 integration tests for MGMT-02 through MGMT-06

## Decisions Made

- AdminHandlerIntegrationTests uses `/DungeonMaster/EditProfile` for AUTH-03 DungeonMasterOnly tests — existing DM-protected route; no new route needed for test coverage
- GroupManagementIntegrationTests.DeleteGroup_WhenHasMembers creates a fresh group and adds a fresh member to it rather than relying on Group 1 (EuphoriaInn) — avoids test ordering/isolation sensitivity
- AddMember test explicitly removes the UserGroups row that CreateAuthenticatedClientWithUserAsync may seed for the test user, ensuring the AddMember POST endpoint is genuinely being tested

## Deviations from Plan

**GetAllPlayers / GetAllDungeonMasters `?? 1` fallback (human verify):** Plan 29-01 introduced `if (groupId == null) return []` in both repository methods. During human verification, `/players` was empty and `/Admin/Users` showed no role badges. Root cause: `SessionKeys.ActiveGroupId` is never written to the HTTP session pre-Phase-30 — the group-picker (Phase 30) is what will populate it at login. Fixed all three `?? 1` sites to match the existing `QuestController.EnqueueSessionReminder ?? 1` pattern.

**Phase 30 must remove these `?? 1` fallbacks:** Once Phase 30 writes `SessionKeys.ActiveGroupId` at login, the fallback is no longer needed and will cause cross-group data leakage in multi-group deployments. Remove from `UserRepository.GetAllDungeonMasters`, `UserRepository.GetAllPlayers`, and `AdminController.Users`. Noted in STATE.md and in code comments at all three sites.

## Issues Encountered

None. All 22 new tests passed on first run without any fixes needed.

## Known Stubs

None. All tests exercise real endpoints and make real DB assertions via service scope.

## Threat Surface Scan

No new network endpoints, auth paths, or schema changes introduced. This plan adds tests only.

Threat mitigations from plan's threat model verified by tests:
- T-29-05-01: SuperAdmin bypass test verifies SuperAdmin → 200 on AdminOnly (AUTH-04) — AdminHandlerIntegrationTests.AdminOnlyPage_WhenSuperAdmin_ShouldReturn200
- T-29-05-02: Non-SuperAdmin denial test verifies 403/redirect on /platform/* (AUTH-05) — PlatformAreaIntegrationTests.PlatformIndex_WhenNotSuperAdmin_ShouldDeny and PlatformIndex_WhenAdmin_ShouldDeny
- T-29-05-03: Duplicate name validation covered by GroupManagementIntegrationTests.GroupsIndex_ShouldShowSeededGroup (EuphoriaInn always present after ClearDatabaseAsync)
- T-29-05-04: HasMembersAsync guard covered by GroupManagementIntegrationTests.DeleteGroup_WhenHasMembers_ShouldRedirectToIndex

## Next Phase Readiness

Phase 29 is complete upon checkpoint approval. The human-verify checkpoint requires:
1. Apply migration and assign SuperAdmin role via SQL (documented in plan)
2. Verify /platform/Group/Index renders with modern-card style and EuphoriaInn in the table
3. Verify group CRUD and member management UI flows
4. Verify non-SuperAdmin receives 403 on /platform/*
5. Verify role badges in /Admin/Users

Phase 30 (Group UX + User Management) can proceed once checkpoint is approved.

---
*Phase: 29-superadmin-role-and-management-area*
*Completed: 2026-06-30*
