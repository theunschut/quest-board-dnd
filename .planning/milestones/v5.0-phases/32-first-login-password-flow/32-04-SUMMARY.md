---
phase: 32-first-login-password-flow
plan: 04
subsystem: admin-user-management
tags: [aspnet-identity, passwordless-account-creation, hangfire-email-jobs]

# Dependency graph
requires: [32-01, 32-02]
provides:
  - "AdminController.CreateUser POST creates passwordless accounts and enqueues WelcomeEmailJob targeting /Account/SetPassword"
  - "AdminController.SendConfirmationEmail retargeted to resend the Welcome email (action name preserved)"
  - "CreateUserViewModel + CreateUser views with no Password field"
  - "AdminController.cs has zero remaining callers of GenerateEmailConfirmationAsync / ConfirmationEmailJob"
affects: [32-05-dead-code-cleanup]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Admin-initiated account creation reuses the GeneratePasswordResetTokenForUserAsync + WebEncoders Base64Url encode/decode idiom already established for other token-carrying email links"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs
    - QuestBoard.Service/Views/Admin/CreateUser.cshtml
    - QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml
    - QuestBoard.Service/Views/Admin/Users.cshtml

key-decisions:
  - "SendConfirmationEmail action name kept unchanged (not renamed) per plan instruction — minimizes churn, keeps Users.cshtml asp-action wiring and any existing integration-test references stable"
  - "Callback URL built with Url.Action(\"SetPassword\", \"Account\", ...) using a string literal, not nameof(AccountController.SetPassword) — Plan 03 (which defines SetPassword) had not yet merged into this worktree branch at execution time, so nameof would not compile; string-literal action names are also the codebase's pre-existing idiom (e.g. the old \"ConfirmEmail\" literal this code replaces). Resolves correctly once both plans merge since MVC routing is action-name-string-based regardless of nameof vs literal."

requirements-completed: [PWFLOW-01, PWFLOW-05]

# Metrics
duration: 25min
completed: 2026-07-01
---

# Phase 32 Plan 04: Admin Passwordless User Creation + Welcome Flow Summary

**Rewrote `AdminController.CreateUser`/`SendConfirmationEmail` to create passwordless accounts and enqueue `WelcomeEmailJob` targeting `/Account/SetPassword`, removing the Password field from the create-user form and its view model, and relabeling the Users.cshtml row action to "Resend Welcome Email".**

## Performance

- **Duration:** 25 min (includes worktree branch recovery)
- **Started:** 2026-07-01
- **Completed:** 2026-07-01
- **Tasks:** 3 completed
- **Files modified:** 5

## Accomplishments

- `CreateUserViewModel` no longer has a `Password` property; both `CreateUser.cshtml` and `CreateUser.Mobile.cshtml` no longer render a password input — account creation is now fully passwordless on the admin side (D-01)
- `AdminController.CreateUser` POST calls the passwordless `userService.CreateAsync(model.Email, model.Name)` (Plan 01 signature), then `identityService.GeneratePasswordResetTokenForUserAsync(userId)` to issue a raw ResetPassword-purpose token, encodes it with the existing `WebEncoders.Base64UrlEncode` idiom, and builds a callback URL to `SetPassword` on `AccountController` before enqueuing `WelcomeEmailJob` (Plan 02) — replacing the old `GenerateEmailConfirmationAsync` + `ConfirmationEmailJob` path entirely (D-06)
- `AdminController.SendConfirmationEmail` retargeted the same way: issues a password-reset token instead of an email-confirmation token, builds the same `SetPassword` callback URL, and enqueues `WelcomeEmailJob` instead of `ConfirmationEmailJob`. The action name **was not renamed** — it remains `SendConfirmationEmail` (D-07/D-14)
- `Users.cshtml`'s unconfirmed-user row button still guards on `!userModel.EmailConfirmed && !string.IsNullOrEmpty(userModel.User.Email)` and still posts to `asp-action="SendConfirmationEmail"`; only the button label (and an adjacent comment) changed to "Resend Welcome Email"
- This resolved the transient build break Plan 01/02 introduced in `AdminController.cs` (`CS1061` on `model.Password`, two `CS0246` on `ConfirmationEmailJob`) — `dotnet build` now succeeds solution-wide with 0 errors
- `AdminController.cs` now has zero references to `GenerateEmailConfirmationAsync` or `ConfirmationEmailJob` — leaves those interface/implementation members callerless, ready for Plan 05 (Wave 3) to delete
- Full test suite green: `QuestBoard.UnitTests` 57/57 passed, `QuestBoard.IntegrationTests` 188/188 passed (including all 11 `AdminControllerIntegrationTests`)

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove Password from CreateUserViewModel and its views** - `159b7fc` (feat)
2. **Task 2: Rewrite CreateUser POST and SendConfirmationEmail to the Welcome flow** - `c0c3584` (feat)
3. **Task 3: Relabel the Users.cshtml row button to Resend Welcome Email** - `1a8d220` (feat)

_Note: Plan metadata commit (this SUMMARY.md) is handled separately by this executor per worktree-mode instructions — STATE.md/ROADMAP.md are excluded and owned by the orchestrator after merge._

## Files Created/Modified

- `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` - Removed the `Password` property block; `Email`/`Name`/`GroupRole` untouched
- `QuestBoard.Service/Views/Admin/CreateUser.cshtml` - Removed the password input `<div>` block; modern-card structure, `<hr>`, and button row unchanged
- `QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml` - Same password input block removed; mobile card wrapper and button row unchanged
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - `CreateUser` POST and `SendConfirmationEmail` both rewritten to the passwordless/Welcome-email flow (see Accomplishments)
- `QuestBoard.Service/Views/Admin/Users.cshtml` - Row-action button label changed to "Resend Welcome Email"; trigger condition and `asp-action` unchanged

## Exact TempData Success Strings (for traceability / Plan 05 test assertions)

- `CreateUser` success: `"Account created for {model.Name}. A welcome email with a set-password link has been sent."`
- `SendConfirmationEmail` success: `"Welcome email queued for {user.Name}."`
- `SendConfirmationEmail` error (unchanged text, still accurate to the failure mode): `"Failed to send confirmation email to {user.Name}. Please try again."`

## Decisions Made

- Kept the `SendConfirmationEmail` action name unchanged per the plan's explicit instruction, to avoid churn in `Users.cshtml`'s `asp-action` wiring and any existing integration-test route references
- Built the `SetPassword` callback URL using the string literal `"SetPassword"` rather than `nameof(AccountController.SetPassword)`. Plan 03 (which adds the `SetPassword` action to `AccountController`) had not yet merged into this worktree branch at the time this plan executed — `AccountController.cs` in this branch still only has `ConfirmEmail`/`ConfirmEmailChange`, so `nameof(AccountController.SetPassword)` would not have compiled. A string-literal action name is also consistent with the pre-existing codebase idiom this code replaces (`"ConfirmEmail"` was likewise a literal, not a `nameof`). MVC action routing resolves by string name regardless, so this will link up correctly once Plan 03 and Plan 04 are both merged onto the same base.

## Deviations from Plan

None requiring a fix beyond the one documented decision above (which is a mechanical necessity of worktree isolation, not a behavioral deviation) — all three tasks were implemented exactly as specified in `32-04-PLAN.md`.

## Auth Gates

None encountered.

## Known Stubs

None — no hardcoded empty values, placeholder text, or unwired data introduced by this plan.

## Threat Flags

None — this plan's only new surface (the `SetPassword` callback URL construction in `AdminController`) is exactly the mitigation `T-32-13` already calls for in the plan's own threat model (uses `GeneratePasswordResetTokenForUserAsync`, ResetPassword-purpose token, never `GenerateEmailConfirmationAsync`). No new endpoints, auth paths, or schema changes were introduced.

## Issues Encountered

**Worktree base mismatch (pre-execution, not a plan deviation):** At agent startup, the worktree branch `worktree-agent-a5e24ce9cf5c14a8d` was found sitting at a stale commit (`2016a01`, an unrelated email-styling merge from `698470d`'s lineage) rather than the expected base `343726f` (phase 32 wave-1 tracking update). Verified via `git merge-base --is-ancestor` that `343726f` is a normal ancestor of `milestone/v5-multi-tenancy` and that the stale commit `2016a01` was itself an ancestor of `343726f` (i.e., strictly older history, not a diverged/protected-branch situation), then ran `git reset --hard 343726f` to align the worktree branch to its expected starting point before any task work began. No task-related files were affected by this reset.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `AdminController.cs` no longer calls `GenerateEmailConfirmationAsync` anywhere — combined with Plan 03 leaving `AccountController.ConfirmEmail` untouched (per this plan's coordination note), the orphaned members after both Wave 2 plans merge are: `IIdentityService`/`IdentityService.GenerateEmailConfirmationAsync`, `IIdentityService`/`IdentityService.ConfirmEmailAsync`, and `AccountController.ConfirmEmail`. Their deletion is entirely Plan 05's responsibility (Wave 3), which should run only after both Plan 03 and Plan 04 have merged and can safely grep solution-wide for zero remaining callers before deleting.
- `dotnet build` (solution-wide, 6 projects) succeeds with 0 errors; `dotnet test QuestBoard.UnitTests` 57/57 green; `dotnet test QuestBoard.IntegrationTests` 188/188 green (including all `AdminControllerIntegrationTests`).
- Since `AccountController.SetPassword` does not yet exist in this worktree branch (Plan 03 not yet merged here), the callback URL was built with a string literal rather than `nameof`. Once Plan 03 and Plan 04 are both merged onto the same branch, verify with a build that `Url.Action("SetPassword", "Account", ...)` resolves to Plan 03's actual `SetPassword` GET action and that the query-param shape (`userId` + Base64Url-encoded `token`) matches exactly — this plan's callback construction was written to match the shape documented in Plan 03's note (`?userId={int}&token={Base64Url-encoded}`), but was not able to run an end-to-end integration test against the real `SetPassword` action since it isn't present in this branch.

---
*Phase: 32-first-login-password-flow*
*Completed: 2026-07-01*

## Self-Check: PASSED

All claimed files exist (32-04-SUMMARY.md) and all claimed commits (159b7fc, c0c3584, 1a8d220, 610715e) are present in git history.
