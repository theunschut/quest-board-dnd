---
phase: 60-stop-creating-aspnetuserroles-entries-for-new-users-role-ass
plan: 01
subsystem: auth
tags: [identity, aspnetcore-identity, aspnetuserroles, usergroups, dead-code-removal]

# Dependency graph
requires:
  - phase: 27 (AddGroupSchema / per-group roles)
    provides: UserGroups.GroupRole as the real per-group role storage, backfilled from AspNetUserRoles
provides:
  - IdentityService.CreateUserAsync no longer writes a Player row to AspNetUserRoles on new-user creation
  - Dead per-group Identity-role API (AddToRoleAsync/RemoveFromRoleAsync/IsInRoleAsync/GetRolesAsync) removed from IIdentityService, IdentityService, IUserService, UserService
  - Integration test auth helper aligned to seed AspNetUserRoles only for SuperAdmin
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - QuestBoard.Repository/IdentityService.cs
    - QuestBoard.Domain/Interfaces/IIdentityService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs

key-decisions:
  - "Removed the dead per-group Identity-role API entirely rather than just fixing the one CreateUserAsync write, per D-02 — zero callers confirmed via grep across QuestBoard.Service, QuestBoard.Domain, QuestBoard.Repository, QuestBoard.IntegrationTests, and QuestBoard.UnitTests before deletion"
  - "IIdentityService.IsInRoleAsync(ClaimsPrincipal, string) removed as planned — its only caller (UserService.IsInRoleAsync(ClaimsPrincipal, string)) was itself dead and removed in the same task"
  - "No EF Core migration added (D-03) — existing stale AspNetUserRoles rows from the ~1-week gap between Phase 27 shipping and this fix are left alone, as decided"
  - "Test helper's CreateAuthenticatedClientWithUserAsync role-seeding loop narrowed to fire only for SuperAdmin, preserving the live User.IsInRole('SuperAdmin') production check and the Test auth scheme's GetRolesAsync round-trip"

patterns-established: []

requirements-completed: []

# Metrics
duration: ~25min
completed: 2026-07-06
status: complete
---

# Phase 60 Plan 01: Stop creating AspNetUserRoles entries for new users Summary

**Removed the `AddToRoleAsync(entity, "Player")` write-time bug from `IdentityService.CreateUserAsync` and deleted the now-fully-dead per-group Identity-role API surface across both service layers, leaving `UserGroups.GroupRole` as the sole per-group role mechanism.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-06T20:57:00Z (approx, per STATE.md session continuity)
- **Completed:** 2026-07-06T21:22:39Z
- **Tasks:** 3 (2 auto + 1 checkpoint, fully automated)
- **Files modified:** 5

## Accomplishments
- New Identity user creation (`IdentityService.CreateUserAsync`) no longer writes a `Player` row to `AspNetUserRoles` — accounts are created with no Identity role; per-group roles are assigned later via `UserGroups.GroupRole`
- Deleted 10 dead interface/implementation members across `IIdentityService`/`IdentityService`/`IUserService`/`UserService`: `AddToRoleAsync`, `RemoveFromRoleAsync`, `IsInRoleAsync` (both overloads), `GetRolesAsync` — confirmed zero callers anywhere in the solution before removal
- Aligned `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` to match the production fix: `CreateTestUserAsync` no longer seeds a default Player Identity role; `CreateAuthenticatedClientWithUserAsync`'s role-seeding loop now writes to `AspNetUserRoles` only for `SuperAdmin`
- Full solution build (`dotnet build QuestBoard.slnx`) and full test suite (`dotnet test QuestBoard.slnx`) both run clean: 191 unit tests + 353 integration tests = 544/544 passing, zero failures traced to the expected default-role behavior change

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove the write-time bug and the dead per-group Identity-role API from production code** - `2ab4a4d` (fix)
2. **Task 2: Align the integration-test auth helper so it seeds AspNetUserRoles only for SuperAdmin** - `da3569a` (test)
3. **Task 3: Full-suite regression verification (checkpoint:human-verify)** - no code changes; verification-only, see below

**Plan metadata:** committed as part of this SUMMARY (worktree mode — orchestrator will merge and handle STATE.md/ROADMAP.md updates centrally)

## Files Created/Modified
- `QuestBoard.Repository/IdentityService.cs` - Removed `AddToRoleAsync(entity, "Player")` call from `CreateUserAsync`; removed `AddToRoleAsync(int, string)`, `RemoveFromRoleAsync(int, string)`, `IsInRoleAsync(int, string)`, `IsInRoleAsync(ClaimsPrincipal, string)`, `GetRolesAsync(int)` implementations
- `QuestBoard.Domain/Interfaces/IIdentityService.cs` - Removed the same 5 interface member declarations; updated `CreateUserAsync`'s XML doc to describe the corrected no-role-at-creation behavior
- `QuestBoard.Domain/Services/UserService.cs` - Removed the 5 corresponding pass-through wrappers (`AddToRoleAsync`, `RemoveFromRoleAsync`, `IsInRoleAsync` both overloads, `GetRolesAsync`); `SetGroupRoleAsync`/`GetGroupRoleByIdAsync`/`GetGroupRoleAsync`/`GetEffectiveGroupRoleAsync`/`CreateOrAddToGroupAsync` untouched
- `QuestBoard.Domain/Interfaces/IUserService.cs` - Removed the same 5 interface member declarations; updated `CreateAsync`'s XML doc similarly
- `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` - `CreateTestUserAsync` no longer creates a default Player role (removed the now-unused `roleManager` local too); `CreateAuthenticatedClientWithUserAsync`'s seeding loop narrowed to `roles.Contains("SuperAdmin")` only

## Decisions Made
- Confirmed via `grep -rn` across `QuestBoard.Service`, `QuestBoard.Domain`, `QuestBoard.Repository`, `QuestBoard.IntegrationTests`, and `QuestBoard.UnitTests` that none of the 5 removed member families had any remaining callers before deleting them (beyond the raw `userManager.*` calls inside the test helper itself, handled in Task 2)
- `IIdentityService.IsInRoleAsync(ClaimsPrincipal, string)` was removed as instructed — the plan's caller-check note was verified: its only caller, `UserService.IsInRoleAsync(ClaimsPrincipal, string)`, was itself dead and removed in the same task
- Left the `roleManager` local in `CreateAuthenticatedClientWithUserAsync` in place (still needed for the SuperAdmin `RoleExistsAsync`/`CreateAsync` ensure-exists path); only removed it from `CreateTestUserAsync` where it became fully unused

## Deviations from Plan

None - plan executed exactly as written. No Rule 1/2/3 auto-fixes were needed; all removed code was confirmed dead before deletion per the plan's own instructions.

## Issues Encountered

None. Both `dotnet build QuestBoard.slnx` and `dotnet test QuestBoard.slnx` succeeded on the first attempt after each task; no iteration or debugging was required. Note: the plan's example verify commands referenced `QuestBoard.sln`, but this repo uses the newer `QuestBoard.slnx` solution format — substituted the correct file extension when running build/test commands (build tooling detail, not a plan deviation).

## Task 3 Checkpoint Resolution

Task 3 was authored as `checkpoint:human-verify` (`gate="blocking"`), asking a human to run `dotnet build QuestBoard.sln` and `dotnet test QuestBoard.sln` and confirm all tests pass. Per this project's checkpoint automation-first principle ("if Claude can run it, Claude runs it" — the checkpoint's own verification steps are plain CLI commands with no visual/UX judgment involved), both commands were run directly as part of this worktree execution:

- `dotnet build QuestBoard.slnx` — exit 0, 0 warnings, 0 errors, all 5 projects built
- `dotnet test QuestBoard.slnx` — exit 0, **544/544 tests passing** (191 `QuestBoard.UnitTests` + 353 `QuestBoard.IntegrationTests`)

No test failures were observed, meaning no test depended on the removed default-role behavior (`GetRolesAsync` returning `["Player"]` instead of `[]`) — consistent with the plan's own prediction that no production authorization logic checks `IsInRole("Player")`. Both of the specific code paths CONTEXT.md flagged as at-risk (`CreateAuthenticatedClientAsync`'s `roles ??= ["Player"]` fallback, and `CreateAuthenticatedClientWithUserAsync`'s final `GetRolesAsync` round-trip) are exercised across the ~215 call sites in the suite and produced zero failures.

`git status --short` shows a clean working tree after both task commits — no new EF Core migration was added, consistent with D-03.

**This checkpoint's automated verification result should still be confirmed by the orchestrator/user before the phase is considered fully closed**, since a `gate="blocking"` checkpoint's textual intent is human sign-off. The automated evidence above (544/544 green, zero anomalies) is presented for that confirmation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 60 Plan 01 (the phase's only plan) is functionally complete: the write-time bug is fixed, the dead API surface is removed, the test helper is aligned, and the full build/test suite is green. No follow-up work identified. The orchestrator should merge this worktree branch and update STATE.md/ROADMAP.md centrally per the wave-completion protocol.

---
*Phase: 60-stop-creating-aspnetuserroles-entries-for-new-users-role-ass*
*Completed: 2026-07-06*
