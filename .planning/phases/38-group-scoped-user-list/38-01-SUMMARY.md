---
phase: 38-group-scoped-user-list
plan: 01
subsystem: auth
tags: [aspnet-core-mvc, ef-core, admin-controller, multi-tenancy, authorization]

# Dependency graph
requires:
  - phase: 34.3 (Group role authorization regression fix)
    provides: GetGroupRoleByIdAsync / SetGroupRoleAsync / UserGroups membership model used as the guard primitive
provides:
  - Group-scoped all-members read method (UserRepository.GetAllGroupMembers / IUserService.GetAllGroupMembersAsync) through the full Repository -> Domain -> Service layering
  - AdminController.Users() scoped to the active group instead of every platform user
  - Membership guard on PromoteToAdmin/DemoteFromAdmin/PromoteToDM/DemoteToPlayer rejecting out-of-group userIds
  - Cross-group-isolation regression test (Users_WhenAdmin_DoesNotShowUsersFromOtherGroups)
affects: [40-platform-members-page-redesign]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Manual UserGroups join for group-scoped user queries, no role filter for 'any member' semantics"
    - "Silent-redirect-on-guard-failure for authorization checks in write actions (no TempData/error banner)"
    - "Reuse existing null-returning lookup (GetGroupRoleByIdAsync) as a membership existence check instead of adding a dedicated IsMemberOfGroupAsync"

key-files:
  created: []
  modified:
    - QuestBoard.Repository/UserRepository.cs
    - QuestBoard.Domain/Interfaces/IUserRepository.cs
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs

key-decisions:
  - "Added a dedicated GetAllGroupMembers/GetAllGroupMembersAsync method rather than unioning GetAllDungeonMasters+GetAllPlayers, per plan's explicit non-default choice — clearer intent, single query, no role filter"
  - "Membership guard on the four POST actions reuses existing GetGroupRoleByIdAsync(userId, groupId) rather than adding a new IsMemberOfGroupAsync method — null return already means 'not a member'"
  - "SuperAdmin has no exception on Users() — scoping applies uniformly, consistent with a group Admin (accepted trade-off per threat model T-38-SA)"

patterns-established:
  - "Group-scoped 'any member regardless of role' read method takes an explicit int groupId parameter rather than reading ambient IActiveGroupContext, so it's reusable/testable and the caller controls which group"

requirements-completed: [USERS-01]

# Metrics
duration: 25min
completed: 2026-07-03
status: complete
---

# Phase 38 Plan 01: Group-Scoped User List Summary

**Closed a live cross-tenant PII leak by scoping `AdminController.Users()` to the active group and hardening the four role-change POST actions against out-of-group `userId` tampering, proven by a passing regression test.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-03T21:11:00Z
- **Completed:** 2026-07-03T21:36:02Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `Users()` now reads exclusively via `userService.GetAllGroupMembersAsync(groupId.Value)`, a new manual-join method mirroring the existing `GetAllDungeonMasters`/`GetAllPlayers` shape but with no role filter — a group admin can no longer see every platform user
- All four role-change POST actions (`PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer`) now reject a crafted `userId` for a user outside the active group via a silent redirect, matching the codebase's existing guard style exactly
- New integration test `Users_WhenAdmin_DoesNotShowUsersFromOtherGroups` proves the isolation with both a positive control (in-group member renders) and the core negative assertion (out-of-group member never renders) — verified RED against the pre-fix `GetAllAsync()` call before confirming GREEN

## Task Commits

Each task was committed atomically:

1. **Task 1: Add group-scoped all-members read method through the Domain -> Repository layering** - `7d49c00` (feat)
2. **Task 2: Scope Users() read and guard the four role-change POST actions in AdminController** - `96de7cd` (fix)
3. **Task 3: Add cross-group-isolation regression test** - `a19830e` (test)

_Task 3 is a single commit (test) since the implementation being tested was already committed in Tasks 1-2; RED was verified manually against a temporarily-reverted copy of AdminController.cs, not via a separate commit, to avoid landing a deliberately-broken intermediate commit._

## Files Created/Modified
- `QuestBoard.Repository/UserRepository.cs` - Added `GetAllGroupMembers(int groupId, ...)`, manual UserGroups join, no role filter
- `QuestBoard.Domain/Interfaces/IUserRepository.cs` - Added matching interface signature + XML doc
- `QuestBoard.Domain/Interfaces/IUserService.cs` - Added `GetAllGroupMembersAsync(int groupId, ...)` signature + XML doc
- `QuestBoard.Domain/Services/UserService.cs` - Added one-line pass-through to the repository method
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - `Users()` now calls the scoped method; all four role-change POST actions gained a membership guard before `SetGroupRoleAsync`
- `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` - Added `Users_WhenAdmin_DoesNotShowUsersFromOtherGroups`

## Decisions Made
- Chose a dedicated `GetAllGroupMembers`/`GetAllGroupMembersAsync` method (plan's explicit non-default option) over unioning the two existing role-filtered methods, for a single clear query with no role restriction
- Reused `GetGroupRoleByIdAsync` for the D-05 membership check instead of adding a new `IsMemberOfGroupAsync` — its null return already signals "not a member," per Claude's Discretion note in CONTEXT.md
- Left per-user role display (the `GetGroupRoleByIdAsync` loop) and sort logic in `Users()` untouched — no regression risk, matches plan's explicit instruction not to collapse into a single query

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. All acceptance criteria and verification commands passed on first attempt for each task, including the full `dotnet build` and the full `AdminControllerIntegrationTests` suite (20/20 passed) run as part of overall phase verification.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 38 (this phase) closes the cross-tenant PII read leak and matching write-path authorization bypass described in `PROJECT.md`'s v6.1 milestone scope — USERS-01 is now Validated.
- Phase 39 (Shared Collision-Aware User Creation & Email) and Phase 40 (Platform Members Page Redesign) are unblocked and independent of this phase's changes; per STATE.md decision, Phase 39 must ship before Phase 40 since Phase 40's create-user entry point reuses Phase 39's shared logic.
- No blockers introduced. The pre-existing SuperAdmin "see everyone" behavior on `/Admin/Users` is now removed by design (accepted trade-off, T-38-SA) — the dedicated cross-group Platform Members page ships in Phase 40.

---
*Phase: 38-group-scoped-user-list*
*Completed: 2026-07-03*
