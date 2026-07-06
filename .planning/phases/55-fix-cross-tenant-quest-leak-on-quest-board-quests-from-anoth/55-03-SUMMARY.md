---
phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
plan: 03
subsystem: auth
tags: [authorization, idor, group-picker, aspnet-core-mvc]

# Dependency graph
requires: []
provides:
  - Object-level membership check on GroupPickerController.SelectGroup, closing an IDOR gap
  - IUserService injected into GroupPickerController (constructor now takes IGroupService + IUserService)
  - Regression test SelectGroup_WhenNotAMember_ShouldReturnNotFound
affects: [55-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SelectGroup mirrors Index's own isSuperAdmin/userId resolution style rather than introducing a new pattern"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs

key-decisions:
  - "Reused the existing IUserService.GetGroupRoleByIdAsync primitive instead of adding a new membership check method"
  - "404 Not Found (not 403) on non-membership, matching this codebase's locked hide-existence convention (Phase 49 D-04/D-09/D-13)"
  - "SuperAdmin bypasses the membership check entirely, matching Index's existing GetAllWithMemberCountAsync any-group listing"

patterns-established: []

requirements-completed: []

# Metrics
duration: 18min
completed: 2026-07-06
---

# Phase 55 Plan 03: GroupPickerController SelectGroup Membership Check Summary

**Closed an IDOR gap in `GroupPickerController.SelectGroup` — POSTing an existing but foreign `groupId` now returns 404 instead of silently setting that group active in session.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-07-06T07:49:00Z
- **Completed:** 2026-07-06T08:07:00Z
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments
- Added a RED integration test proving the current controller trusted `groupId` existence alone
- Injected `IUserService` into `GroupPickerController` and added a membership check via `GetGroupRoleByIdAsync`, returning 404 for non-members while preserving the SuperAdmin any-group bypass
- Verified the fix does not regress the existing happy-path (`SelectGroup_ShouldPersistActiveGroupInSession`) or any other test in the file — all 8 tests green

## Task Commits

Each task was committed atomically:

1. **Task 1: Write failing non-member SelectGroup 404 integration test (Wave 0)** - `9e870a6` (test)
2. **Task 2: Add membership check to SelectGroup + inject IUserService** - `dcf51ea` (feat)

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator merges and finalizes shared state)

_Note: Task 1 followed a RED/GREEN handoff — the test committed in Task 1 stayed failing until Task 2's feat commit made it pass._

## Files Created/Modified
- `QuestBoard.Service/Controllers/GroupPickerController.cs` - Constructor now injects `IUserService` alongside `IGroupService`; `SelectGroup` verifies caller membership via `GetGroupRoleByIdAsync` before writing `ActiveGroupId` to session, returning 404 for non-members (SuperAdmin exempt)
- `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` - New `SelectGroup_WhenNotAMember_ShouldReturnNotFound` test seeding a second group with no membership row for the authenticated user

## Decisions Made
- Reused `IUserService.GetGroupRoleByIdAsync(userId, groupId)` (null = not a member) rather than adding a new service method — this primitive was already DI-registered app-wide and used by `AdminController`/`AccountController`
- Kept the fix scoped strictly to the membership check; the D-06 validated-at timestamp work is deliberately left for Plan 04 per the plan's own scoping note

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `SelectGroup` is now safe against cross-tenant group-id tampering; no known follow-up required from this plan
- Plan 04 (D-06 validated-at timestamp work) can proceed independently — it touches the same controller area but a different concern

---
*Phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth*
*Completed: 2026-07-06*
