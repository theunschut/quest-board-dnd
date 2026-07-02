---
phase: 32-first-login-password-flow
verified: 2026-07-01T00:00:00Z
status: passed
score: 12/12 must-haves verified
overrides_applied: 0
---

# Phase 32: First-Login Password Flow Verification Report

**Phase Goal:** Admin-created users set their own password via a password-reset link in the welcome email; removes admin-set password from CreateUser form; adds a self-service Forgot Password flow.
**Verified:** 2026-07-01
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Admin-created accounts have no password at creation (PWFLOW-01) | VERIFIED | `IdentityService.CreateUserAsync` (`QuestBoard.Repository/IdentityService.cs:33-51`) calls `userManager.CreateAsync(entity)` (no-password overload); `CreateUserViewModel.cs` has no `Password` property; `CreateUser.cshtml`/`CreateUser.Mobile.cshtml` contain no password input (grep confirmed zero matches) |
| 2 | A newly created user receives one combined Welcome email; clicking the link sets password AND confirms email in one `SetPassword` action (PWFLOW-02) | VERIFIED | `AdminController.CreateUser` enqueues `WelcomeEmailJob` targeting `/Account/SetPassword`; `AccountController.SetPassword` POST calls `ResetPasswordAsync` then `ConfirmEmailDirectlyAsync` on success; integration test `SetPassword_Post_WithValidToken_ShouldSetPasswordAndConfirmEmail` asserts both `EmailConfirmed == true` and the new password validates — test passes |
| 3 | A passwordless account cannot sign in until SetPassword is completed (PWFLOW-03) | VERIFIED | `PasswordSignInAsync` wraps `SignInManager` (fails gracefully by Identity design against null `PasswordHash`); integration test `Login_Post_PasswordlessAccount_ShouldNotSignIn` asserts no crash (`<500`) and no redirect to authenticated area — passes |
| 4 | Self-service Forgot Password flow sends a reset link landing on SetPassword; POST is enumeration-safe and rate-limited 3/15min per IP (PWFLOW-04) | VERIFIED | `AccountController.ForgotPassword` POST sets identical `TempData["Success"]` message regardless of `userId.HasValue`; `[EnableRateLimiting("forgot-password")]` applied; `Program.cs` registers `"forgot-password"` fixed-window policy (`PermitLimit=3`, `Window=15min`); integration tests `ForgotPassword_Post_KnownAndUnknownEmail_ShouldReturnSameGenericMessage` and `ForgotPassword_Post_ExceedingRateLimit_ShouldReturn429` both pass |
| 5 | Admin "Resend welcome email" button (EmailConfirmed==false) sends the new Welcome email; old ConfirmEmail flow retired (PWFLOW-05) | VERIFIED | `Users.cshtml:142` gates button on `!userModel.EmailConfirmed`; `AdminController.SendConfirmationEmail` enqueues `WelcomeEmailJob` (not `ConfirmationEmailJob`); `ConfirmationEmailJob.cs`, `ConfirmEmail.razor`, `ConfirmationEmailJobTests.cs`, `AccountController.ConfirmEmail`, `IIdentityService/IdentityService.GenerateEmailConfirmationAsync`/`ConfirmEmailAsync` all confirmed deleted; solution-wide grep for `GenerateEmailConfirmationAsync`/`ConfirmationEmailJob` returns zero matches |
| 6 | `DataProtectionTokenProviderOptions.TokenLifespan` configured to 7 days (PWFLOW-06) | VERIFIED | `Program.cs:68-71`: `builder.Services.Configure<DataProtectionTokenProviderOptions>(options => { options.TokenLifespan = TimeSpan.FromDays(7); });` |
| 7 | GeneratePasswordResetTokenForUserAsync uses ResetPassword-purpose token, never email-confirmation purpose | VERIFIED | `IdentityService.GeneratePasswordResetTokenForUserAsync` (line 112-117) calls `userManager.GeneratePasswordResetTokenAsync(entity)` exclusively; no `GenerateEmailConfirmationTokenAsync` call in this method |
| 8 | ConfirmEmailDirectlyAsync sets EmailConfirmed=true via direct property write, no token verification | VERIFIED | `IdentityService.ConfirmEmailDirectlyAsync` (line 126-134): `entity.EmailConfirmed = true; return await userManager.UpdateAsync(entity);` — no `IUserEmailStore` cast, no token check |
| 9 | Login page shows "Forgot password?" link (D-08) | VERIFIED | `Login.cshtml` and `Login.Mobile.cshtml` both contain `<a asp-action="ForgotPassword" ...>Forgot password?</a>` |
| 10 | The email-address-change flow (separate from the retired confirm-email flow) remains untouched | VERIFIED | `AccountController.ConfirmEmailChange`, `IIdentityService.GenerateChangeEmailTokenAsync`/`ChangeEmailAsync`, `IdentityService.GenerateChangeEmailTokenAsync`/`ChangeEmailAsync` all present and unmodified in structure |
| 11 | Full automated test suite green (blocking gate before phase completion) | VERIFIED | Independently re-ran `dotnet build` (0 errors) and `dotnet test` (58 unit + 196 integration = 254 tests, 0 failures) — reproduces the SUMMARY's claimed count exactly |
| 12 | Human verification of email rendering and full click-through flow | VERIFIED | `32-HUMAN-UAT.md`: 8/8 items pass, including two real bugs found and fixed mid-checkpoint (Welcome copy accuracy for legacy accounts, reverse-proxy `X-Forwarded-For` trust) — both fixes independently confirmed present in code (`Welcome.razor` `IsNewAccount` param, `HasPasswordAsync` wiring, `Program.cs` `UseForwardedHeaders()` + `ReverseProxy:KnownProxies` config, `docs/server-setup.md` deploy instructions) |

**Score:** 12/12 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/IdentityService.cs` | Passwordless CreateUserAsync + token/confirm primitives | VERIFIED | All methods present, correct token purpose, `HasPasswordAsync` added |
| `QuestBoard.Domain/Services/UserService.cs` | Pass-through wrappers | VERIFIED | `GeneratePasswordResetTokenForUserAsync`, `ConfirmEmailDirectlyAsync`, `HasPasswordAsync` all delegate correctly |
| `QuestBoard.Service/Jobs/WelcomeEmailJob.cs` | Welcome email dispatch via scope | VERIFIED | Uses `CreateAsyncScope()`, renders `Welcome` with `IsNewAccount` param (added post-plan for legacy-account copy fix) |
| `QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs` | Forgot-password email dispatch | VERIFIED | Uses `CreateAsyncScope()`, no `userName` param (D-11 compliant) |
| `QuestBoard.Service/Components/Emails/Welcome.razor` | Welcome template | VERIFIED | Cinzel/wax-seal style, "Set My Password" CTA, `IsNewAccount` branch for copy variants |
| `QuestBoard.Service/Components/Emails/ForgotPassword.razor` | Forgot-password template | VERIFIED | Distinct template, "Reset My Password" CTA, "did not request" disclaimer present |
| `QuestBoard.Service/Program.cs` | TokenLifespan 7d + rate limiter + UseRateLimiter | VERIFIED | All three present; plus post-plan `UseForwardedHeaders()`/`ReverseProxy:KnownProxies` fix |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` | ForgotPassword/SetPassword anonymous actions | VERIFIED | Both flows present, enumeration-safe, rate-limited (dedicated `set-password` policy added beyond original plan per WR-02 fix) |
| `QuestBoard.Service/ViewModels/AccountViewModels/ForgotPasswordViewModel.cs` | Email-only model | VERIFIED | Single `Email` field, `[Required][EmailAddress]` |
| `QuestBoard.Service/ViewModels/AccountViewModels/SetPasswordViewModel.cs` | UserId/Token/NewPassword/Compare, MinimumLength 8 | VERIFIED | Confirmed `MinimumLength = 8` (not the buggy 6) |
| `QuestBoard.Service/Views/Account/ForgotPassword.cshtml` / `SetPassword.cshtml` (+ Mobile) | Anonymous views | VERIFIED | All four views present, correct model binding, no Layout override |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | Passwordless CreateUser + Welcome resend | VERIFIED | `CreateUser` POST 2-arg `CreateAsync`, enqueues `WelcomeEmailJob`; `SendConfirmationEmail` server-side guards `EmailConfirmed` (WR-03 fix applied beyond original plan scope) |
| `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` | No Password property | VERIFIED | Only `Email`/`Name`/`GroupRole` |
| `QuestBoard.Service/Views/Admin/Users.cshtml` | Resend-welcome button gated on EmailConfirmed==false | VERIFIED | Button label "Resend Welcome Email", condition unchanged |
| `QuestBoard.Domain/Interfaces/IIdentityService.cs` (absence check) | `GenerateEmailConfirmationAsync` removed | VERIFIED | Zero matches; `ConfirmEmailAsync` also removed; `ChangeEmail*` flow intact |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` (absence check) | `ConfirmEmail` GET removed | VERIFIED | Zero matches for the deleted action; `ConfirmEmailChange` intact |
| `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` | ForgotPassword/SetPassword/passwordless-login coverage | VERIFIED | 8 new tests present and passing (confirmed via direct read + full suite run) |
| `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` | Adapted Welcome-resend coverage | VERIFIED | `CreateUser` test adapted (no Password field), `SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess` added |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `UserService.cs` | `IdentityService.cs` | identityService delegation | WIRED | All three new methods delegate through Domain→Repository |
| `WelcomeEmailJob.cs` | `Welcome.razor` | `RenderAsync<Welcome>` | WIRED | Confirmed in job source, includes new `IsNewAccount` key |
| `ForgotPasswordEmailJob.cs` | `ForgotPassword.razor` | `RenderAsync<ForgotPassword>` | WIRED | Confirmed in job source |
| `Program.cs` | forgot-password rate limit policy | `AddPolicy` fixed-window | WIRED | Policy registered, `UseRateLimiter()` in pipeline after `UseRouting()` |
| `AccountController.cs` | `ForgotPasswordEmailJob` | `jobClient.Enqueue` | WIRED | `Enqueue<ForgotPasswordEmailJob>` called only inside `userId.HasValue` branch |
| `AccountController.cs` | `IIdentityService.GeneratePasswordResetTokenForUserAsync` | token issuance | WIRED | Called in both `ForgotPassword` POST and `SetPassword` uses `ResetPasswordAsync` to verify |
| `Login.cshtml` | ForgotPassword action | `asp-action="ForgotPassword"` | WIRED | Confirmed present in both desktop and mobile views |
| `AdminController.cs` | `WelcomeEmailJob` | `jobClient.Enqueue` | WIRED | Both `CreateUser` and `SendConfirmationEmail` enqueue `WelcomeEmailJob` |
| `AdminController.cs` | `AccountController.SetPassword` | `Url.Action` callback | WIRED | Callback URL correctly targets `SetPassword` with `userId`+`token` query params matching Plan 03's shape |
| `CreateUser.cshtml` | `CreateUserViewModel` | binding without Password | WIRED | Confirmed no `asp-for="Password"` anywhere |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `WelcomeEmailJob` | `callbackUrl`, `isNewAccount` | `Url.Action(...)` (real route), `HasPasswordAsync` (real DB query via `UserManager`) | Yes | FLOWING |
| `ForgotPasswordEmailJob` | `callbackUrl` | `Url.Action(...)` built from a real password-reset token | Yes | FLOWING |
| `AccountController.SetPassword` | `model.UserId`/`model.Token` | Query string round-tripped through hidden form fields, decoded via `WebEncoders.Base64UrlDecode`, verified by `UserManager.ResetPasswordAsync` | Yes | FLOWING |
| `Users.cshtml` resend button | `userModel.EmailConfirmed` | `AdminController.Users()` populates from `user.EmailConfirmed` (real entity field) | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds | `dotnet build` | 6 projects, 0 errors, 34 pre-existing/unrelated warnings | PASS |
| Full test suite passes | `dotnet test` | 58 unit + 196 integration = 254 tests, 0 failures | PASS |
| No dead-code references remain | `grep -rn "GenerateEmailConfirmationAsync\|ConfirmationEmailJob"` across `*.cs`/`*.razor`/`*.cshtml` | 0 matches | PASS |
| No debt markers in phase files | `grep "TBD\|FIXME\|XXX"` across AccountController/AdminController/jobs/Program.cs | 0 matches | PASS |

### Probe Execution

Step 7c: SKIPPED — no `scripts/*/tests/probe-*.sh` convention used by this project; phase is a standard ASP.NET Core controller/service phase verified via `dotnet build`/`dotnet test`, not a migration/CLI-probe phase.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|--------------|-------------|--------------|--------|----------|
| PWFLOW-01 | 32-01, 32-04 | Admin-created accounts have no password at creation | SATISFIED | Passwordless `CreateUserAsync`; `CreateUserViewModel`/views have no Password field |
| PWFLOW-02 | 32-01, 32-02, 32-03 | Combined Welcome email sets password + confirms email in one action | SATISFIED | `SetPassword` POST sequences `ResetPasswordAsync` → `ConfirmEmailDirectlyAsync`; integration test passes |
| PWFLOW-03 | 32-01, 32-03 | Passwordless account cannot sign in gracefully | SATISFIED | `Login_Post_PasswordlessAccount_ShouldNotSignIn` passes |
| PWFLOW-04 | 32-02, 32-03 | Forgot-password enumeration-safe + rate-limited 3/15min | SATISFIED | Both integration tests pass; Program.cs policy confirmed |
| PWFLOW-05 | 32-02, 32-04 | Resend-welcome button + old ConfirmEmail flow retired | SATISFIED | Dead code fully deleted; button relabeled and functional |
| PWFLOW-06 | 32-02 | 7-day TokenLifespan | SATISFIED | Confirmed in Program.cs |

**Note (documentation gap, not a functional gap):** `.planning/REQUIREMENTS.md` still lists PWFLOW-01 through PWFLOW-06 as unchecked `- [ ]` checkboxes (lines 67-72) and the Traceability table (lines 133-138) still shows status "Planned — 32-0X" for all six, rather than "Complete". This is inconsistent with `ROADMAP.md`, which correctly shows Phase 32 as `[x]` complete with all 5 plans `[x]` complete, and with the actual codebase state verified above (all six requirements are functionally satisfied). No orphaned requirements were found — all six PWFLOW IDs are accounted for across the five plans.

### Anti-Patterns Found

None found in phase-modified files. No `TBD`/`FIXME`/`XXX` markers, no placeholder/stub returns, no empty handlers. The code-review process (32-REVIEW.md) already found and the fix pass (32-REVIEW-FIX.md) already resolved 9 of 11 findings (0 critical, 5 warnings, 6 info); the 2 skipped findings (IN-03: unused logger/cancellationToken in email jobs; IN-04: timing side-channel on enumeration-safety) are both explicitly endorsed as low-priority/not-worth-fixing by the review itself, with rationale documented in 32-REVIEW-FIX.md. These are acceptable deferrals, not defects.

### Human Verification Required

None — `32-HUMAN-UAT.md` already documents a completed 8/8-pass human verification checkpoint (dated the same day, matching the phase's own blocking `checkpoint:human-verify` gate in Plan 05 Task 4). No additional human verification items were identified during this codebase-based verification pass.

### Gaps Summary

No functional gaps found. All 6 PWFLOW requirements are implemented, wired, and covered by passing automated tests; the full suite (254 tests) is green; dead code is fully retired with zero solution-wide references remaining; the human UAT checkpoint passed 8/8 with two genuine bugs found and fixed mid-checkpoint (both independently confirmed present in the current codebase). The only issue identified is a documentation-sync gap: `.planning/REQUIREMENTS.md`'s checkboxes and Traceability table for PWFLOW-01..06 were not updated to reflect completion (they still read "Planned"), unlike `ROADMAP.md` which is correctly marked complete. This is cosmetic/administrative and does not block phase completion, but should be corrected so future phase-planning greps of REQUIREMENTS.md don't misreport Phase 32 as outstanding.

---

_Verified: 2026-07-01T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
