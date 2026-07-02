---
phase: 32-first-login-password-flow
plan: 01
subsystem: auth
tags: [aspnet-identity, password-reset-token, passwordless-account-creation]

# Dependency graph
requires: []
provides:
  - "Passwordless CreateUserAsync(email, name) across IIdentityService/IdentityService/IUserService/UserService"
  - "GeneratePasswordResetTokenForUserAsync(userId) — raw ResetPassword-purpose token by userId, null for unknown user"
  - "ConfirmEmailDirectlyAsync(userId) — direct EmailConfirmed = true property write + UpdateAsync"
affects: [32-03-forgot-password-flow, 32-04-admin-welcome-flow]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Direct EmailConfirmed property write + UpdateAsync (no token verification) for post-reset confirmation"
    - "Raw password-reset token exposed by userId for email-link embedding (Base64Url-encoded by the caller)"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/IIdentityService.cs
    - QuestBoard.Repository/IdentityService.cs
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs

key-decisions:
  - "CreateUserAsync signature changed in place (not a new overload) — single caller (AdminController.CreateUser) confirmed by RESEARCH.md; caller break deferred to Plan 04 by design"
  - "Two granular methods chosen over one combined method: GeneratePasswordResetTokenForUserAsync (token issuance) and ConfirmEmailDirectlyAsync (post-verification flag set) — matches RESEARCH.md Open Question 2 resolution recorded in the plan"

patterns-established:
  - "Pattern: token-purpose correctness — GeneratePasswordResetTokenForUserAsync always uses GeneratePasswordResetTokenAsync (purpose ResetPassword), never GenerateEmailConfirmationTokenAsync, so downstream ResetPasswordAsync verification succeeds"

requirements-completed: [PWFLOW-01, PWFLOW-02, PWFLOW-03]

# Metrics
duration: 12min
completed: 2026-07-01
---

# Phase 32 Plan 01: Backend Service-Layer Primitives Summary

**Passwordless `CreateUserAsync` plus new `GeneratePasswordResetTokenForUserAsync`/`ConfirmEmailDirectlyAsync` primitives added across `IIdentityService`→`IdentityService`→`IUserService`→`UserService`, using the ResetPassword token purpose throughout.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-01T11:51:00Z
- **Completed:** 2026-07-01T12:03:00Z
- **Tasks:** 2 completed
- **Files modified:** 4

## Accomplishments
- Account creation is now passwordless: `CreateUserAsync(string email, string name)` calls `userManager.CreateAsync(entity)` (no-password overload), leaving `PasswordHash` null until a future `SetPassword` flow runs
- Added `GeneratePasswordResetTokenForUserAsync(int userId)` — issues a raw `ResetPassword`-purpose token by userId (returns null for an unknown user), verified via the same token provider `AdminResetPasswordAsync` already uses
- Added `ConfirmEmailDirectlyAsync(int userId)` — sets `EmailConfirmed = true` via direct property write + `UpdateAsync`, with no token verification and no `IUserEmailStore` casting
- `QuestBoard.Domain` and `QuestBoard.Repository` both build with 0 errors

## Task Commits

Each task was committed atomically:

1. **Task 1: Passwordless CreateUserAsync across all four layers** - `8f2165f` (feat)
2. **Task 2: Add GeneratePasswordResetTokenForUserAsync + ConfirmEmailDirectlyAsync** - `9d36649` (feat)

_Note: Plan metadata commit is handled separately by this executor per worktree-mode instructions (SUMMARY.md only; STATE.md/ROADMAP.md excluded)._

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IIdentityService.cs` - Dropped `password` param from `CreateUserAsync`; added `GeneratePasswordResetTokenForUserAsync`/`ConfirmEmailDirectlyAsync` signatures
- `QuestBoard.Repository/IdentityService.cs` - Passwordless `CreateUserAsync` implementation; new `GeneratePasswordResetTokenForUserAsync`/`ConfirmEmailDirectlyAsync` implementations
- `QuestBoard.Domain/Interfaces/IUserService.cs` - Dropped `password` param from `CreateAsync`; added matching wrapper signatures
- `QuestBoard.Domain/Services/UserService.cs` - Dropped `password` arg from `CreateAsync` pass-through; added one-line delegation wrappers for both new methods

## Decisions Made
- Kept `CreateUserAsync`/`CreateAsync` signature changes in place rather than adding parallel overloads, since RESEARCH.md confirmed `IUserService.CreateAsync` is the only caller of `IIdentityService.CreateUserAsync`, and `AdminController.CreateUser` is the only caller of `IUserService.CreateAsync` — the resulting single caller break in the Service project is expected and explicitly deferred to Plan 04 (Wave 2)
- Implemented two granular methods (`GeneratePasswordResetTokenForUserAsync` + `ConfirmEmailDirectlyAsync`) rather than one combined method, per the plan's chosen resolution of RESEARCH.md's Open Question 2 — keeps `ResetPasswordAsync` untouched and lets the Plan 03 controller sequence both calls explicitly

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

**Worktree base mismatch (pre-execution, not a plan deviation):** At agent startup, the worktree branch `worktree-agent-a587fdd104c434a38` was found sitting at a stale commit (`2016a01`, an unrelated email-styling merge) rather than the expected base `4e91a0c` (phase 32 plan creation). Verified via `git merge-base` that the expected base was a normal forward descendant on `milestone/v5-multi-tenancy` (not a diverged/protected-ref situation), then `git reset --hard 4e91a0c` to align the worktree branch to its expected starting point before any task work began. No task-related files were affected by this reset since no prior commits existed on the stale worktree branch beyond the initial state.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `IIdentityService`/`IUserService` now expose the exact method signatures Plans 03 (self-service forgot-password) and 04 (admin welcome flow) depend on: `CreateUserAsync(email, name)`, `GeneratePasswordResetTokenForUserAsync(userId)`, `ConfirmEmailDirectlyAsync(userId)`
- Known, expected, and documented: `AdminController.CreateUser` (Service project) will fail to compile until Plan 04 updates its call site to the new 2-arg `CreateAsync` signature — this is a Wave 2 concern, not a blocker for this plan
- `dotnet test` was not run in this plan (no test project changes were made; RESEARCH.md's Wave 0 Gaps for `IdentityServiceTests.cs` remain open for a future plan to address)

---
*Phase: 32-first-login-password-flow*
*Completed: 2026-07-01*

## Self-Check: PASSED

All claimed files exist (IIdentityService.cs, IdentityService.cs, IUserService.cs, UserService.cs, 32-01-SUMMARY.md) and all claimed commits (8f2165f, 9d36649, 47fad65) are present in git history.
