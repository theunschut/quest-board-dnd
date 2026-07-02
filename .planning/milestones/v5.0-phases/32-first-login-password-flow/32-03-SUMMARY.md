---
phase: 32-first-login-password-flow
plan: 03
subsystem: account-controller-user-facing-flow
tags: [aspnet-identity, forgot-password, set-password, enumeration-safety, rate-limiting]

# Dependency graph
requires: [32-01, 32-02]
provides:
  - "AccountController.ForgotPassword GET/POST — enumeration-safe (D-11), rate-limited via [EnableRateLimiting(\"forgot-password\")]"
  - "AccountController.SetPassword GET/POST — route/query shape: userId (int) + token (string) as query params on GET, UserId/Token as hidden fields round-tripped on POST"
  - "Login.cshtml / Login.Mobile.cshtml now link to ForgotPassword (D-08)"
affects: [32-04-admin-welcome-flow, 32-05-dead-code-retirement]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Enumeration-safe POST: identical TempData message + redirect regardless of whether GetIdByEmailAsync found a user"
    - "SetPassword success sequences ResetPasswordAsync then ConfirmEmailDirectlyAsync only after Succeeded — email confirmation cannot be forced without proving control of a valid reset token"

key-files:
  created:
    - QuestBoard.Service/ViewModels/AccountViewModels/ForgotPasswordViewModel.cs
    - QuestBoard.Service/ViewModels/AccountViewModels/SetPasswordViewModel.cs
    - QuestBoard.Service/Views/Account/ForgotPassword.cshtml
    - QuestBoard.Service/Views/Account/ForgotPassword.Mobile.cshtml
    - QuestBoard.Service/Views/Account/SetPassword.cshtml
    - QuestBoard.Service/Views/Account/SetPassword.Mobile.cshtml
  modified:
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.Service/Views/Account/Login.cshtml
    - QuestBoard.Service/Views/Account/Login.Mobile.cshtml

key-decisions:
  - "Used identityService.ResetPasswordAsync(int userId, string token, string newPassword) directly (the userId-taking IIdentityService overload) rather than fetching a User object first and calling the IUserService pass-through — avoids an extra GetByIdAsync round trip since SetPasswordViewModel already carries UserId"
  - "SetPassword uses a single centered d-grid submit button (Login.cshtml's single-button pattern), not ChangePassword's two-button d-flex justify-content-between — this is an anonymous landing page with no natural Cancel destination, per plan guidance"
  - "Login.Mobile.cshtml / ForgotPassword.Mobile.cshtml follow the account-card-mobile pattern (matching Login) rather than admin-form-card-mobile; SetPassword.Mobile.cshtml includes an explicit @Html.AntiForgeryToken() since it is a single-purpose anonymous form with no surrounding authenticated session context"

requirements-completed: [PWFLOW-02, PWFLOW-03, PWFLOW-04]

# Metrics
duration: 25min
completed: 2026-07-01
---

# Phase 32 Plan 03: First-Login Forgot/Set-Password User Flow Summary

**Added the self-service Forgot Password + shared SetPassword landing page to `AccountController` (enumeration-safe, rate-limited, token-purpose-pure), with matching ViewModels, desktop/mobile views, and a "Forgot password?" link on Login — zero dead-code deletion.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-01 (Wave 2)
- **Completed:** 2026-07-01
- **Tasks:** 3 completed
- **Files modified/created:** 10 (6 created, 4 modified)

## Accomplishments

- `ForgotPasswordViewModel` (single `Email` field) and `SetPasswordViewModel` (`UserId`/`Token`/`NewPassword`/`ConfirmPassword`, `MinimumLength = 8` — matching `Program.cs`'s `Password.RequiredLength = 8`, not the buggy `MinimumLength = 6` used by existing analogs) created.
- `AccountController` gained four anonymous actions:
  - `ForgotPassword` GET renders the email-entry form.
  - `ForgotPassword` POST is enumeration-safe (D-11) — sets the identical `TempData["Success"] = "If that email is registered, a reset link has been sent."` and redirects to itself regardless of whether the email matched an account; only enqueues `ForgotPasswordEmailJob` inside the `userId.HasValue` branch. Guarded by `[EnableRateLimiting("forgot-password")]` (Plan 02's policy).
  - `SetPassword` GET accepts `userId`/`token` query params and round-trips them into hidden form fields via the view model.
  - `SetPassword` POST decodes the Base64Url token, calls `identityService.ResetPasswordAsync(model.UserId, decodedToken, model.NewPassword)`; on success calls `identityService.ConfirmEmailDirectlyAsync(model.UserId)` (D-09) and redirects to Login; on failure surfaces `IdentityResult.Errors` to `ModelState`; a try/catch around the token decode redirects to Login with a generic error on tampered/expired tokens.
- Four new views (`ForgotPassword.cshtml`/`.Mobile.cshtml`, `SetPassword.cshtml`/`.Mobile.cshtml`) render under the standard `_Layout` (no override — confirmed via `_ViewStart.cshtml`), following `modern-card`/`modern-card-header`/`modern-card-body` conventions.
- `Login.cshtml` and `Login.Mobile.cshtml` each gained a "Forgot password?" link (`asp-action="ForgotPassword"`) between the RememberMe checkbox and the submit button.
- No dead-code deletion — `AccountController.ConfirmEmail` GET remains in place (Plan 05's job in Wave 3).

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ForgotPasswordViewModel + SetPasswordViewModel** - `b9aeaca` (feat)
2. **Task 2: Add ForgotPassword + SetPassword actions to AccountController** - `a4a160f` (feat)
3. **Task 3: Create ForgotPassword + SetPassword views; add Login link** - `82f5e21` (feat)

_Note: Plan metadata commit is handled separately by this executor per worktree-mode instructions (SUMMARY.md only; STATE.md/ROADMAP.md excluded)._

## Files Created/Modified

- `QuestBoard.Service/ViewModels/AccountViewModels/ForgotPasswordViewModel.cs` (new) - single `[Required][EmailAddress] Email` field
- `QuestBoard.Service/ViewModels/AccountViewModels/SetPasswordViewModel.cs` (new) - `UserId`/`Token`/`NewPassword` (`MinimumLength = 8`)/`ConfirmPassword` (`[Compare]`)
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` (modified) - added `ForgotPassword` GET/POST, `SetPassword` GET/POST; added `using Microsoft.AspNetCore.RateLimiting;`
- `QuestBoard.Service/Views/Account/ForgotPassword.cshtml` (new) - anonymous single-email-field card
- `QuestBoard.Service/Views/Account/ForgotPassword.Mobile.cshtml` (new) - mobile `account-card-mobile` equivalent
- `QuestBoard.Service/Views/Account/SetPassword.cshtml` (new) - anonymous password-set form with hidden `UserId`/`Token`
- `QuestBoard.Service/Views/Account/SetPassword.Mobile.cshtml` (new) - mobile equivalent with explicit `@Html.AntiForgeryToken()`
- `QuestBoard.Service/Views/Account/Login.cshtml` (modified) - added "Forgot password?" link
- `QuestBoard.Service/Views/Account/Login.Mobile.cshtml` (modified) - added "Forgot password?" link

## Route/Query-Param Shape (for Plan 04)

`SetPassword` GET route: `/Account/SetPassword?userId={int}&token={Base64Url-encoded-string}`

This is the exact shape `AccountController.ForgotPassword` POST already builds via:
```csharp
Url.Action(nameof(SetPassword), "Account", new { userId = userId.Value, token = encodedToken }, Request.Scheme)
```
Plan 04's `WelcomeEmailJob` callback URL (built by `AdminController.CreateUser`) must match this identical `userId`/`token` query-param shape and Base64Url encoding idiom (`WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken))`) so both the welcome-email flow and the forgot-password flow land on the same `SetPassword` action.

## Enumeration-Safe Message Text (confirmed)

`"If that email is registered, a reset link has been sent."` — appears exactly once in `AccountController.cs`, outside any `userId.HasValue` branch, set identically whether or not `GetIdByEmailAsync` found a match.

## Decisions Made

- Used the `IIdentityService.ResetPasswordAsync(int userId, string token, string newPassword)` overload directly in `SetPassword` POST rather than fetching a `User` domain object first and calling `IUserService.ResetPasswordAsync(User, ...)` — `SetPasswordViewModel.UserId` is already available, so the direct `userId` overload avoids an unnecessary `GetByIdAsync` call.
- `SetPassword` views use a single centered `d-grid` submit button (mirroring `Login.cshtml`), not `ChangePassword`'s two-button `d-flex justify-content-between` — this is an anonymous single-purpose landing page with no natural "Cancel" destination, so no fabricated Cancel link was added.
- Mobile views follow the `account-card-mobile` wrapper (matching `Login.Mobile.cshtml`) rather than `admin-form-card-mobile` (used by admin-area forms) — closer analog for an anonymous account-flow page.

## Deviations from Plan

None - plan executed exactly as written.

## Known Pre-Existing Build State (not introduced by this plan)

`dotnet build QuestBoard.Service` currently fails with 3 pre-existing errors in `AdminController.cs` (`CS1501` at line 113 — `CreateAsync` 3-arg overload removed by Plan 01; `CS0246` at lines 127 and 287 — `ConfirmationEmailJob` type removed by Plan 02). These are explicitly documented in `32-02-SUMMARY.md`'s "Known Limitation" section as Plan 04's responsibility (Plan 04 rewrites `AdminController.CreateUser`/`SendConfirmationEmail`). This plan's own objective explicitly forbids touching `IIdentityService.cs`/`IdentityService.cs` and does not touch `AdminController.cs` either, so these errors are unrelated to and unaffected by this plan's changes.

Verified: ran `dotnet build QuestBoard.Service` after each task — the error set was identical (same 3 errors, same line numbers) before and after every change in this plan, confirming zero new errors were introduced.

`dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~AccountControllerIntegrationTests` cannot execute for the same reason — `QuestBoard.IntegrationTests` references `QuestBoard.Service`, so the whole `QuestBoard.Service` project must build first. This is expected transient Wave 2 state; Plan 04 (same wave) resolves it.

## Auth Gates

None encountered.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `AccountController.SetPassword` GET/POST route/query shape (`userId` + `token`, Base64Url-encoded) is now live for Plan 04's `WelcomeEmailJob` callback URL to target.
- `AccountController.ConfirmEmail` GET action remains in place and untouched — Plan 05 (Wave 3) removes it once Plan 04 also stops calling `GenerateEmailConfirmationAsync`.
- Once Plan 04 lands and fixes the 3 pre-existing `AdminController.cs` errors, `dotnet build QuestBoard.Service` and the full `AccountControllerIntegrationTests` filter should both go green with this plan's changes included.

---
*Phase: 32-first-login-password-flow*
*Completed: 2026-07-01*

## Self-Check: PASSED

All claimed files exist (ForgotPasswordViewModel.cs, SetPasswordViewModel.cs, AccountController.cs, ForgotPassword.cshtml, ForgotPassword.Mobile.cshtml, SetPassword.cshtml, SetPassword.Mobile.cshtml, Login.cshtml, Login.Mobile.cshtml, 32-03-SUMMARY.md) and all claimed commits (b9aeaca, a4a160f, 82f5e21, a4570f2) are present in git history.
