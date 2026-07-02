---
phase: 33-session-persistence-persist-activegroupid-across-app-restart
plan: 02
subsystem: admin
tags: [rate-limiting, partitioned-rate-limiter, admin-controller, email]

# Dependency graph
requires: []
provides:
  - Singleton PartitionedRateLimiter<int> in Program.cs, keyed email-resend:{userId}, 3/hour fixed window
  - AttemptAcquire(userId) guard in AdminController.SendConfirmationEmail
  - AttemptAcquire(model.Id) guard inside AdminController.EditUser's emailChanged branch
affects: [33-03]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Programmatic PartitionedRateLimiter<TKey> + AttemptAcquire for actions where the partition key is a POST form field rather than a route value (AddRateLimiter policies run before MVC model binding)"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Program.cs
    - QuestBoard.Service/Controllers/Admin/AdminController.cs

key-decisions:
  - "Used a programmatic PartitionedRateLimiter<int> singleton + AttemptAcquire instead of an AddRateLimiter policy — userId/Id are POST form fields, not route values, so the policy-factory path (which runs before MVC model binding) cannot read them"
  - "EditUser's guard placed inside the emailChanged branch only — a non-email-changing save is not counted against the budget (D-07)"
  - "CreateUser left untouched — one-shot automated welcome email on account creation is explicitly exempt (D-08)"

requirements-completed: [EMAIL-RATE-01, EMAIL-RATE-02, EMAIL-RATE-03, EMAIL-RATE-04]

# Metrics
duration: 2min
completed: 2026-07-01
status: complete
---

# Phase 33 Plan 02: Admin Email Rate Limiting Summary

**Rate-limited the repeatable manual admin email-send buttons (SendConfirmationEmail, EditUser email-change) to 3/hour per target user via a programmatic `PartitionedRateLimiter<int>`, protecting the Resend relay's 100/day quota while leaving CreateUser's one-shot welcome email untouched.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-07-01T16:45:11Z
- **Completed:** 2026-07-01T16:46:50Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Registered a singleton `PartitionedRateLimiter<int>` in `Program.cs` via `PartitionedRateLimiter.Create<int, string>(...)`, fixed-window `PermitLimit = 3`, `Window = TimeSpan.FromHours(1)`, `QueueLimit = 0`, `AutoReplenishment = true`, partition key `$"email-resend:{userId}"` — placed after the existing `AddRateLimiter` block (forgot-password/set-password policies), which was left completely unchanged
- Constructor-injected the limiter into `AdminController` as `emailResendLimiter`
- Added `AttemptAcquire(userId)` as the first statement of `SendConfirmationEmail`, returning `Status429TooManyRequests` + `"Too many requests. Please try again later."` on rejection (matching the shape of the existing `OnRejected` hook, which this manual path bypasses)
- Added `AttemptAcquire(model.Id)` as the first statement inside `EditUser`'s `emailChanged && !string.IsNullOrEmpty(model.Email)` branch — a save that doesn't change the email is never counted
- Verified `CreateUser` is untouched (grep confirms no `AttemptAcquire` in its body)
- Ran the 12 existing `AdminControllerIntegrationTests` — all pass, confirming the new constructor parameter resolves correctly through the app's real DI container (the singleton registered in `Program.cs` is what `WebApplicationFactory`-based tests exercise)

## Task Commits

Each task was committed atomically:

1. **Task 1: Register the admin-email-resend PartitionedRateLimiter singleton** - `5835a7a` (feat)
2. **Task 2: Enforce the limiter in SendConfirmationEmail and EditUser's email-change branch** - `b50dc39` (feat)

**Plan metadata:** pending (docs: complete plan)

## Files Created/Modified
- `QuestBoard.Service/Program.cs` - Added singleton `PartitionedRateLimiter<int>` registration (3/hour, keyed `email-resend:{userId}`), placed after the existing `AddRateLimiter` block; that block and its two policies + `OnRejected` hook are unchanged
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - Added `using System.Threading.RateLimiting;`, a `PartitionedRateLimiter<int> emailResendLimiter` constructor parameter, an `AttemptAcquire(userId)` guard at the top of `SendConfirmationEmail`, and an `AttemptAcquire(model.Id)` guard inside `EditUser`'s email-change branch

## Decisions Made
- Followed the plan's corrected approach (D-06 correction) exactly: programmatic `AttemptAcquire` rather than the literal `[EnableRateLimiting]`/`GetRouteValue` text originally captured in 33-CONTEXT.md, since `userId`/`Id` are POST form fields on this project's default route, not route values.
- EditUser's guard is scoped strictly to the email-change branch (D-07) — placing it at method entry would have incorrectly counted saves that don't touch the email address.
- CreateUser intentionally left untouched (D-08).

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. The rate limiter is in-process (singleton), requires no new infrastructure, and takes effect on next app restart/deploy.

## Next Phase Readiness

- Code-level implementation of EMAIL-RATE-01..04 is complete and builds successfully.
- Behavioral verification (429 on the 4th resend within the hour, independent per-target-user budgets, EditUser email-change counted vs. non-email-change not counted, CreateUser exempt) is deferred to Plan 03's test/checkpoint work, per this plan's `<verification>` section.
- Plan 03 can proceed independently — no blocking dependency from this plan beyond the code now existing in `Program.cs`/`AdminController.cs`.

---
*Phase: 33-session-persistence-persist-activegroupid-across-app-restart*
*Completed: 2026-07-01*

## Self-Check: PASSED

All modified files confirmed present on disk; both task commits (5835a7a, b50dc39) confirmed in git log via `git log --oneline`.
