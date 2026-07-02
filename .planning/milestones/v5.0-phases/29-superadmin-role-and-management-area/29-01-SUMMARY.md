---
phase: 29-superadmin-role-and-management-area
plan: "01"
subsystem: authorization
tags: [auth, user-management, role-system, superadmin, group-roles]
dependency_graph:
  requires:
    - Phase 27 (UserGroups schema — GroupRole column, FK constraints)
    - Phase 28 (IActiveGroupContext dual-registration, MutableGroupContext test stub)
  provides:
    - Working AdminHandler with SuperAdmin bypass and UserGroups.GroupRole check
    - Working DungeonMasterHandler with SuperAdmin bypass and UserGroups.GroupRole check
    - GetGroupRoleAsync / GetGroupRoleByIdAsync / SetGroupRoleAsync on IUserService
    - GetAllPlayers / GetAllDMs querying UserGroups instead of empty AspNetUserRoles
    - SuperAdminOnly authorization policy registered in Program.cs
    - AdminController promote/demote using SetGroupRoleAsync on UserGroups
  affects:
    - Phase 29-04 (Platform MVC Area uses SuperAdminOnly policy)
    - Phase 29-05 (integration tests for new auth handlers and platform area)
tech_stack:
  added: []
  patterns:
    - Three-step authorization handler: SuperAdmin bypass → null group guard → UserGroups.GroupRole check
    - IActiveGroupContext constructor injection in auth handlers (no DI registration change required)
    - Test UserGroups seeding in AuthenticationHelper alongside AspNetUserRoles to keep tests green
key_files:
  created: []
  modified:
    - QuestBoard.Service/Authorization/AdminHandler.cs
    - QuestBoard.Service/Authorization/DungeonMasterHandler.cs
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Interfaces/IUserRepository.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Repository/UserRepository.cs
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/Program.cs
    - QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs
    - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
decisions:
  - AdminHandler and DungeonMasterHandler rewritten with three-step logic per D-01 through D-04
  - IUserService extended with GetGroupRoleAsync (ClaimsPrincipal overload), GetGroupRoleByIdAsync (userId overload), SetGroupRoleAsync
  - UserRepository injects IActiveGroupContext; GetAllPlayers and GetAllDMs filter by UserGroups.GroupRole for active group
  - SuperAdminOnly policy uses RequireRole("SuperAdmin") — no custom handler needed
  - Test AuthenticationHelper updated to seed UserGroups rows for DM/Admin test users (required by new handler logic)
metrics:
  duration: "8 minutes"
  completed_date: "2026-06-30"
  tasks_completed: 3
  files_modified: 10
---

# Phase 29 Plan 01: Auth Handler Rewrite and UserGroups Integration Summary

Auth system restored by rewriting AdminHandler and DungeonMasterHandler to use three-step SuperAdmin bypass → null group guard → UserGroups.GroupRole lookup, replacing the broken AspNetUserRoles path cleared in Phase 27.

## Tasks Completed

| # | Name | Commit | Files |
|---|------|--------|-------|
| 1+2 | Rewrite handlers + extend IUserService/IUserRepository/UserService/UserRepository | 4cf618d | AdminHandler.cs, DungeonMasterHandler.cs, IUserService.cs, IUserRepository.cs, UserService.cs, UserRepository.cs |
| 3 | Fix AdminController, add SuperAdminOnly policy, fix test infrastructure | 2044224 | AdminController.cs, Program.cs, AuthenticationHelper.cs, TenantIsolationTests.cs |

## What Was Built

**AdminHandler (fully replaced):** Three-step logic — `context.User.IsInRole("SuperAdmin")` → succeed immediately; `activeGroupContext.ActiveGroupId is null` → fail; `GetGroupRoleAsync(context.User, groupId) == GroupRole.Admin` → succeed, else fail. Drops the pre-existing buggy `!context.User.Identity?.IsAuthenticated == true` check entirely.

**DungeonMasterHandler (fully replaced):** Same three-step pattern; step 3 accepts `GroupRole.Admin OR GroupRole.DungeonMaster`.

**IUserService extensions:** Added `GetGroupRoleAsync(ClaimsPrincipal, int groupId)`, `GetGroupRoleByIdAsync(int userId, int groupId)`, `SetGroupRoleAsync(int userId, int groupId, GroupRole role)` — all delegating through `IUserRepository` → `UserRepository`.

**UserRepository:** Injected `IActiveGroupContext`; replaced `GetAllDungeonMasters` and `GetAllPlayers` to query `UserGroups.GroupRole` for the active group (returning `[]` when `ActiveGroupId` is null). Added `GetGroupRoleAsync(int, int)` and `SetGroupRoleAsync(int, int, GroupRole)` implementations using `DbContext.UserGroups`.

**AdminController:** Added `IActiveGroupContext activeGroupContext` constructor parameter. Users action now populates `IsAdmin`/`IsDungeonMaster`/`IsPlayer` via `GetGroupRoleByIdAsync` for the active group. Four promote/demote actions rewritten to call `SetGroupRoleAsync` instead of `AddToRoleAsync`/`RemoveFromRoleAsync`.

**Program.cs:** Added `.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"))` to `AddAuthorizationBuilder` chain.

## Verification Results

- `dotnet build QuestBoard.Service` — exits 0
- `dotnet test` — 197/197 tests pass (55 unit + 142 integration)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Pre-existing xUnit v3 IAsyncLifetime return type mismatch in TenantIsolationTests**
- **Found during:** Task 3 (running integration test suite for verification)
- **Issue:** `TenantIsolationTests.InitializeAsync()` and `DisposeAsync()` declared as `Task` return types; xUnit v3 requires `ValueTask`. Build error prevented integration tests from compiling.
- **Fix:** Changed both methods to return `ValueTask` / `ValueTask.CompletedTask`
- **Files modified:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`
- **Commit:** 2044224

**2. [Rule 1 - Bug] Integration tests for DM/Admin auth failed after handler rewrite**
- **Found during:** Task 3 (integration test run)
- **Issue:** 18 tests failed because `CreateAuthenticatedClientWithUserAsync` added users to `AspNetUserRoles` only, but the new handlers exclusively read `UserGroups.GroupRole`. Test DM/Admin users had no `UserGroups` rows for group 1, so all DM/Admin authorization checks returned null → fail.
- **Fix:** Added `UserGroups` seeding in `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync` — maps "Admin" → `GroupRole.Admin`, "DungeonMaster" → `GroupRole.DungeonMaster`, default → `GroupRole.Player` for group ID 1.
- **Files modified:** `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs`
- **Commit:** 2044224

**Note on task grouping:** Tasks 1 and 2 were committed together (4cf618d). The plan explicitly states: "build will fail until Task 2 completes; write both tasks before building." This is correct — AdminHandler calls `GetGroupRoleAsync` which doesn't exist on IUserService until Task 2 extends it.

## Known Stubs

None. All functionality is fully wired.

## Threat Surface Scan

No new network endpoints, auth paths, or schema changes introduced. `SuperAdminOnly` policy uses ASP.NET Core's built-in `RequireRole` — no custom handler registered. All threat mitigations from the plan's `<threat_model>` are applied:
- T-29-01: Three-step handler with explicit `context.Fail()` on null group — implemented
- T-29-02: Explicit `else context.Fail()` when `GetGroupRoleAsync` returns null — implemented
- T-29-03: `[ValidateAntiForgeryToken]` on all promote/demote POSTs; groupId read from server-side `activeGroupContext` — implemented
- T-29-04: DI auto-resolves new constructor params at startup — accepted risk, no action

## Self-Check: PASSED

All 10 modified files exist on disk. Both commits (4cf618d, 2044224) confirmed in git log. All 197 tests pass.
