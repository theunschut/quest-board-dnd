---
phase: 41-safe-user-removal-account-disable
plan: 04
subsystem: auth
tags: [aspnet-core-identity, lockout, login, identityservice]

# Dependency graph
requires:
  - phase: 41-safe-user-removal-account-disable (plan 02)
    provides: IIdentityService.GetLockoutEndAsync/DisableUserAsync/EnableUserAsync primitives built on Identity's LockoutEnd field
provides:
  - AccountController.Login IsLockedOut branch that distinguishes a disabled account (LockoutEnd == DateTimeOffset.MaxValue) from an ordinary temporary lockout
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Login lockout messaging composes GetIdByEmailAsync + GetLockoutEndAsync post-IsLockedOut, using an exact == DateTimeOffset.MaxValue sentinel comparison rather than a fuzzy threshold check"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs

key-decisions:
  - "Test users for the two new integration tests are seeded directly via UserManager.CreateAsync with UserName == Email (not AuthenticationHelper.CreateTestUserAsync, which intentionally uniquifies UserName and Email with different suffixes) — Login resolves the posted Email as a username lookup via SignInManager.PasswordSignInAsync, so a mismatched UserName/Email pair makes the account unresolvable and always yields the generic 'Invalid login attempt.' message instead of exercising the lockout branch at all"

patterns-established: []

requirements-completed: [SAFE-04]

# Metrics
duration: 12min
completed: 2026-07-04
status: complete
---

# Phase 41 Plan 04: Login Disabled-vs-Lockout Messaging Summary

**`AccountController.Login`'s `IsLockedOut` branch now composes `GetIdByEmailAsync` + `GetLockoutEndAsync` and branches on an exact `== DateTimeOffset.MaxValue` comparison to show "This account has been disabled. Contact an administrator." for a disabled account while leaving the "...Try again in 15 minutes." copy unchanged for an ordinary temporary lockout.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-04T12:48:00Z
- **Completed:** 2026-07-04T13:00:22Z
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments
- Added two new integration tests (`Login_Post_DisabledAccount_ShowsDisabledMessage`, `Login_Post_TemporaryLockout_ShowsFifteenMinuteMessage`) that assert the two lockout message paths are mutually exclusive
- `AccountController.Login`'s `IsLockedOut` branch re-resolves the target user via the already-injected `identityService` and picks the message via an exact `DateTimeOffset.MaxValue` equality check (D-13) — no threshold/fuzzy comparison
- Full solution test suite (135 unit + 283 integration = 418 tests) passes with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add disabled-vs-lockout login message integration tests (Wave 0 scaffold, SAFE-04/D-13)** - `6387176` (test)
2. **Task 2: Distinguish disabled vs. temporary lockout in Login's IsLockedOut branch (D-13)** - `5761a50` (fix)

_Note: no plan-metadata commit in worktree mode — orchestrator handles STATE.md/ROADMAP.md updates centrally after the wave completes._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - `Login`'s `IsLockedOut` branch now resolves `lockoutEnd` via `GetIdByEmailAsync` + `GetLockoutEndAsync` and selects between the disabled-account message and the existing 15-minute lockout message based on an exact `MaxValue` comparison
- `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` - added `Login_Post_DisabledAccount_ShowsDisabledMessage` and `Login_Post_TemporaryLockout_ShowsFifteenMinuteMessage`, each seeding a user directly via `UserManager.CreateAsync` with `UserName == Email` and a matching `LockoutEnd`

## Decisions Made
- Test users seed `UserName == Email` directly through `UserManager.CreateAsync` rather than reusing `AuthenticationHelper.CreateTestUserAsync` (which uniquifies `UserName` and `Email` with independent suffixes) — discovered during Task 1 execution that `AccountController.Login` calls `PasswordSignInAsync(model.Email, ...)`, which `SignInManager` resolves as a **username** lookup (`UserManager.FindByNameAsync`), not an email lookup. A mismatched pair makes the seeded account unresolvable at sign-in time, which surfaced as both new tests failing with the generic "Invalid login attempt." message instead of any lockout-branch message, even though `LockoutEnabled`/`LockoutEnd` were confirmed correctly set on the entity (verified via a temporary debug assertion, since removed). This is a pre-existing characteristic of the login flow, not a bug introduced by this plan, so no production code was touched for it (out of scope per plan's file list) — Rule 1/3 auto-fix applied only to the test arrange code.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Corrected test-user seeding to use UserName == Email**
- **Found during:** Task 1 (integration test authoring)
- **Issue:** `AuthenticationHelper.CreateTestUserAsync(userName, email, ...)` intentionally appends independent unique suffixes to `userName` and `email`, producing a `UserEntity` where `UserName != Email`. `AccountController.Login` signs in via `PasswordSignInAsync(model.Email, ...)`, and `SignInManager.PasswordSignInAsync` resolves its first argument as a **username**, not an email. With mismatched `UserName`/`Email`, the login attempt can never resolve the seeded user, so both new tests failed with "Invalid login attempt." regardless of the `LockoutEnd` value set — blocking verification of either message path.
- **Fix:** Seeded each test's user directly via `UserManager.CreateAsync(new UserEntity { UserName = email, Email = email, ... }, password)` instead of `AuthenticationHelper.CreateTestUserAsync`, matching the existing pattern already used by `Login_Post_PasswordlessAccount_ShouldNotSignIn` in the same file.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs`
- **Verification:** Both tests pass after the fix; full `AccountControllerIntegrationTests` suite (23 tests) green.
- **Committed in:** `6387176` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Test-arrange-only fix; no production code touched beyond the plan's specified `IsLockedOut` branch change. No scope creep.

## Issues Encountered

None beyond the auto-fixed test-seeding issue documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- SAFE-04 fully satisfied: a disabled account's login attempt shows the accurate disabled message; an ordinary temporary lockout is unaffected
- Full solution build (`dotnet build`) succeeds with 0 warnings, 0 errors; full test suite (418 tests) passes
- No blockers

---
*Phase: 41-safe-user-removal-account-disable*
*Completed: 2026-07-04*

## Self-Check: PASSED

All modified files confirmed present on disk; both task commit hashes (`6387176`, `5761a50`) confirmed in git log.
