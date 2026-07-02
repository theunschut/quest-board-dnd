---
phase: 31-unauthenticated-landing-redirect
plan: 01
subsystem: auth
tags: [aspnet-identity, authorize, mvc, access-control]

# Dependency graph
requires:
  - phase: 29-superadmin-management-area
    provides: Group-scoped auth handlers (AdminHandler, DungeonMasterHandler) and existing [Authorize] policy conventions (ShopController pattern)
provides:
  - Class-level [Authorize] on CalendarController and QuestLogController — unauthenticated requests now 302 to /Account/Login
  - DungeonMasterController.Profile and GetDMProfilePicture no longer publicly reachable — class-level [Authorize] governs both
affects: [31-02-unauthenticated-landing-redirect, 31-04-unauthenticated-landing-redirect]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bare class-level [Authorize] (no policy argument) for group-scoped controllers with no elevated-role requirement — matches ShopController/GuildMembersController convention (D-01)"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
    - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs

key-decisions:
  - "Bare [Authorize] (no policy) on Calendar/QuestLog — matches D-01, no elevated role required, just authentication"
  - "using Microsoft.AspNetCore.Authorization; retained in DungeonMasterController after AllowAnonymous removal — still needed by class-level [Authorize] and both DungeonMasterOnly-policy EditProfile actions"

patterns-established:
  - "Access-control lockdown via attribute-only changes (no controller logic, no new tests in this plan) — behavioral verification deferred to a dedicated test-update plan in the same phase"

requirements-completed: [UX-04]

# Metrics
duration: 2min
completed: 2026-07-01
---

# Phase 31 Plan 01: Controller Authorize Lockdown Summary

**Class-level `[Authorize]` added to CalendarController and QuestLogController; both `[AllowAnonymous]` overrides removed from DungeonMasterController's DM-profile actions, closing three previously-open group-scoped routes.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-07-01T06:11:39Z
- **Completed:** 2026-07-01T06:13:42Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `CalendarController` and `QuestLogController` now require authentication at the class level — anonymous requests to `/Calendar` and `/QuestLog` will 302 to `/Account/Login` (enforced by ASP.NET Identity's cookie auth challenge, verified behaviorally in plan 31-04)
- `DungeonMasterController.Profile` and `GetDMProfilePicture` no longer carry `[AllowAnonymous]` — the controller's existing class-level `[Authorize]` now covers all four of its actions uniformly
- Solution builds clean (0 errors) after both changes, confirming no downstream compile break from the attribute changes

## Task Commits

Each task was committed atomically:

1. **Task 1: Add class-level [Authorize] to CalendarController and QuestLogController** - `3142edc` (feat)
2. **Task 2: Remove [AllowAnonymous] from DungeonMasterController profile actions** - `58f3e47` (fix)

**Plan metadata:** commit pending (docs: complete plan) — added by orchestrator in worktree-mode metadata step

_Note: No TDD tasks in this plan — both are `type="auto"` attribute-only changes; per-controller integration test updates are deferred to plan 31-04 in the same phase._

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - Added `using Microsoft.AspNetCore.Authorization;` and class-level `[Authorize]` above `CalendarController`
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` - Added class-level `[Authorize]` above `QuestLogController` (import already present)
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` - Removed `[AllowAnonymous]` from `Profile(int id, ...)` and `GetDMProfilePicture(int id, ...)`; class-level `[Authorize]` and both `EditProfile` `[Authorize(Policy = "DungeonMasterOnly")]` attributes left unchanged

## Decisions Made
- Bare `[Authorize]` (no policy argument) used for both Calendar and QuestLog — matches D-01's "no global fallback policy" guidance and mirrors the existing `ShopController` reference pattern; these controllers require only authentication, not an elevated role
- `using Microsoft.AspNetCore.Authorization;` kept in `DungeonMasterController.cs` after removing both `[AllowAnonymous]` attributes — the import is still consumed by the class-level `[Authorize]` and the two `EditProfile` `[Authorize(Policy = "DungeonMasterOnly")]` attributes

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] NuGet restore required before first build in fresh worktree**
- **Found during:** Task 1 (first `dotnet build` attempt)
- **Issue:** `dotnet build --no-restore` failed with `NETSDK1004: Assets file 'project.assets.json' not found` — the worktree checkout had no `obj/` directory populated, since it was freshly created by the isolation harness and never restored
- **Fix:** Ran `dotnet restore` at the solution root (all 5 projects restored successfully; pre-existing NU1510 "will not be pruned" warnings on QuestBoard.Domain are unrelated to this task and were present before any code change)
- **Files modified:** None (restore only regenerates `obj/project.assets.json` and related cache files, not tracked in git)
- **Verification:** Subsequent `dotnet build --no-restore` succeeded with 0 errors
- **Committed in:** N/A — restore artifacts are gitignored, no commit needed

---

**Total deviations:** 1 auto-fixed (1 blocking — environment setup, not a code issue)
**Impact on plan:** No scope creep. The restore was a one-time environment-setup step for the fresh worktree checkout, not a change to plan scope or acceptance criteria.

## Issues Encountered
None — both tasks matched their acceptance criteria on the first attempt after the restore was resolved.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three `must_haves.truths` from the plan frontmatter are satisfied at the attribute level: unauthenticated `/Calendar`, `/QuestLog`, and `/DungeonMaster/Profile/{id}` requests will now hit ASP.NET Identity's auth challenge and redirect (302) to the login page — full behavioral confirmation (actual 302 response codes) is explicitly deferred to plan 31-04's integration test updates, per this plan's `<verification>` section
- No blockers for plan 31-02 (HomeController/QuestController lockdown, per this plan's task-1 note) or plan 31-04 (test updates for these three controllers)
- Threat register entries T-31-01 and T-31-02 are both mitigated as designed; T-31-SC (package installs) N/A — no packages installed this plan

---
*Phase: 31-unauthenticated-landing-redirect*
*Completed: 2026-07-01*

## Self-Check: PASSED

All created/modified files confirmed present on disk; all three task/plan commit hashes (`3142edc`, `58f3e47`, `e60bc19`) confirmed present in `git log --oneline --all`.
