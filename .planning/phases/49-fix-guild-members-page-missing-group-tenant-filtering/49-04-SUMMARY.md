---
phase: 49-fix-guild-members-page-missing-group-tenant-filtering
plan: 04
subsystem: api
tags: [efcore, authorization, multi-tenancy, playersignup]

# Dependency graph
requires:
  - phase: 49-01
    provides: corrected QuestBoardContext comment documenting PlayerSignup scoping invariants
provides:
  - Quest-including PlayerSignup lookup (GetByIdWithQuestAsync) on repository and service
  - Target-Quest group-membership check on QuestController.RemovePlayerSignup, closing a cross-group Admin deletion path
  - Regression tests documenting which PlayerSignup repository lookups are self-scoping and which are not
affects: [quest-signup-removal, admin-authorization]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Load-then-compare-GroupId pattern for confused-deputy fixes: load the target with its group-owning parent Included, compare parent.GroupId to activeGroupContext.ActiveGroupId, 404 (not 403) on mismatch or null group, before any mutation"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs
    - QuestBoard.Repository/PlayerSignupRepository.cs
    - QuestBoard.Domain/Interfaces/IPlayerSignupService.cs
    - QuestBoard.Domain/Services/PlayerSignupService.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs

key-decisions:
  - "GetByIdWithQuestAsync Includes the required Quest navigation, which folds QuestEntity's own group query filter into the join — the lookup is itself group-scoped as defense-in-depth, in addition to the controller's explicit GroupId comparison"
  - "Cross-group and null-active-group both return 404 (not 403) from RemovePlayerSignup, matching the phase's existing information-disclosure convention (hide cross-tenant existence)"

patterns-established:
  - "Quest-including lookup + explicit GroupId comparison before mutation is now the standing pattern for any controller action that takes a raw child-entity id whose authorization depends on the parent's tenant"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-07-05
status: complete
---

# Phase 49 Plan 04: PlayerSignup Cross-Group Deletion Fix Summary

**Closed a confused-deputy vulnerability where an Admin in one group could delete another group's PlayerSignup by guessing its id, via a new Quest-including repository lookup and an explicit GroupId check in `QuestController.RemovePlayerSignup`.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-05T18:31:00Z
- **Completed:** 2026-07-05T18:54:52Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- Added `GetByIdWithQuestAsync` on `IPlayerSignupRepository`/`PlayerSignupRepository` (Includes the parent `Quest`) and mirrored it on `IPlayerSignupService`/`PlayerSignupService`
- `QuestController.RemovePlayerSignup` now loads the signup via the new Quest-including lookup and returns 404 (no deletion) when `activeGroupContext.ActiveGroupId` is null or differs from `signup.Quest.GroupId`, closing the one independently-exploitable PlayerSignup leak (T-49-09/T-49-10)
- Extended `PlayerSignupRepositoryTests.cs` with 3 new tests proving the Quest-including lookup is group-scoped and the base `GetByIdAsync` is not, documenting the caller-must-pre-validate invariant for the phase's other three unfiltered `PlayerSignup` repository methods (T-49-11)

## Task Commits

1. **Task 1: Add a Quest-including PlayerSignup lookup on repository + service** - `b9988a8` (feat)
2. **Task 2: Add target-Quest group-membership check to RemovePlayerSignup** - `ffc3fb6` (fix)
3. **Task 3: Extend PlayerSignupRepositoryTests with cross-group regression coverage** - `637bc1c` (test)

_Note: no plan-metadata commit in worktree mode — the orchestrator commits SUMMARY.md separately after merge._

## Files Created/Modified

- `QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs` - Declares `GetByIdWithQuestAsync(int id, CancellationToken)`
- `QuestBoard.Repository/PlayerSignupRepository.cs` - Implements `GetByIdWithQuestAsync` with `.Include(ps => ps.Quest)`
- `QuestBoard.Domain/Interfaces/IPlayerSignupService.cs` - Declares the matching service method
- `QuestBoard.Domain/Services/PlayerSignupService.cs` - Delegates `GetByIdWithQuestAsync` to the repository
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - `RemovePlayerSignup` loads via the new lookup and checks `signup.Quest.GroupId` against the active group before `RemoveAsync`
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` - Adds a mutable `IActiveGroupContext` test double, a `groupId` parameter on the seed helper, and 3 new tests

## Decisions Made

- Kept the Quest-including lookup's own group-scoping (via the folded-in Quest filter) as defense-in-depth alongside the controller's explicit `GroupId` comparison, rather than relying on either alone — mirrors the plan's stated UserTransaction pattern.
- Used 404 uniformly for both "no active group" and "cross-group target" outcomes in `RemovePlayerSignup`, consistent with the rest of the phase's existing 404-not-403 convention.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `RemovePlayerSignup` is now correctly scoped; no further action needed for this specific endpoint.
- The regression tests in `PlayerSignupRepositoryTests.cs` document (but do not fix) that `GetByIdAsync`, `ChangeVoteAsync`, and `GetTopWaitlistedCandidateAsync` remain caller-dependent for group scoping — any future refactor of their callers should re-verify pre-validation is still in place.
- `dotnet build` succeeds; `QuestBoard.UnitTests` (13/13 in `PlayerSignupRepositoryTests`) and `QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController"` (25/25) both pass with no regressions.
- Grep for `D-1[0-9]`/`Phase 49` across all edited files returns zero matches.

---
*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Completed: 2026-07-05*

## Self-Check: PASSED

- FOUND: .planning/phases/49-fix-guild-members-page-missing-group-tenant-filtering/49-04-SUMMARY.md
- FOUND: b9988a8 (Task 1 commit)
- FOUND: ffc3fb6 (Task 2 commit)
- FOUND: 637bc1c (Task 3 commit)
- FOUND: 96e6252 (SUMMARY commit)
